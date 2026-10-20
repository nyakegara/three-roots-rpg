using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Combat.Pathfinding;
using Game.Core.Campaigns;
using Game.Core.Combat.Generation;
using Game.Core.Content;
using Game.Core.Geometry;
using Game.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame.Screens;

/// <summary>Playable tactical battle. Rules and turn ownership remain in Game.Core.</summary>
internal sealed class BattleScreen : IGameScreen
{
    private readonly PixelCanvas _canvas;
    private readonly AssetCatalog _assets;
    private readonly UnitCatalog _catalog;
    private readonly PixelFont _font;
    private readonly BattleHud _hud;
    private readonly Action _exit;
    private readonly int _seed;
    private readonly CampaignBattle? _campaignBattle;
    private readonly Func<EncounterResolution>? _returnToMap;
    private string? _resultError;
    private readonly AStarPathfinder _pathfinder = new();
    private BattleInteraction _interaction = null!;
    private BattlefieldRenderer _field = null!;
    private HashSet<HexCoord> _reachable = [];
    private HashSet<HexCoord> _attackArea = [];
    private MouseState _currentMouse;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private HexCoord? _hover;
    private bool _showGrid = true;
    private bool _healMode;
    private float _nightBlend;
    private float _nightTarget;
    private float _opponentDelay = 0.7f;
    private string? _actionUnitId;
    private string? _actionTargetId;
    private bool _actionSecondary;
    private float _actionSeconds;
    private BattleAttackEffect? _attackEffect;
    private string _message = "ЛКМ: ПОКАЗАТЬ ПУТЬ. ПОВТОРНАЯ ЛКМ: ПЕРЕМЕСТИТЬ ОТРЯД.";
    private Battle Battle => _interaction.Battle;
    private static readonly Color Gold = new(218, 190, 113);
    private static readonly Color Green = new(149, 190, 107);
    private static readonly Color Red = new(210, 118, 89);
    private static readonly Color Blue = new(125, 195, 185);
    private static readonly int[] EdgeByNeighbor = [1, 0, 5, 4, 3, 2];
    private const float ActionSpriteScale = 1.5f;
    private static readonly Rectangle RestartButton = new(444, 488, 186, 34);
    private static readonly Rectangle ExitButton = new(650, 488, 186, 34);
    private static readonly Rectangle ReturnButton = new(517, 488, 246, 34);

    public BattleScreen(PixelCanvas canvas, AssetCatalog assets, UnitCatalog catalog,
        int seed, bool night, Action exit, BattleCaptureStage capture = BattleCaptureStage.Opening)
        : this(canvas, assets, catalog, exit, night)
    {
        _seed = seed;
        Restart();
        if (capture == BattleCaptureStage.Range)
        {
            for (var step = 0; step < Battle.Units.Count * 2 &&
                 Battle.ActiveUnit?.UnitTypeId != "flora.sprout"; step++)
            {
                Battle.EndActivation();
                if (Battle.ActiveUnit is null) Battle.StartRound();
            }
            RefreshReachable();
            _hover = _attackArea.Where(cell => !_reachable.Contains(cell) &&
                    Battle.Units.All(unit => unit.Position != cell))
                .OrderBy(cell => cell.DistanceTo(Battle.ActiveUnit!.Position))
                .LastOrDefault();
            _message = "ЗЕЛЕНЫЙ: ХОД. ЗОЛОТО: ДАЛЬНОСТЬ. КРАСНЫЙ: ДОСТУПНАЯ ЦЕЛЬ.";
            return;
        }
        if (capture == BattleCaptureStage.Target)
        {
            _hover = BattleCaptureSetup.Advance(Battle, capture);
            RefreshReachable();
            _message = "КРАСНЫЙ КОНТУР: ЦЕЛЬ МОЖНО АТАКОВАТЬ СЕЙЧАС.";
            return;
        }
        if (capture == BattleCaptureStage.RangedTarget)
        {
            _hover = BattleCaptureSetup.Advance(Battle, capture);
            RefreshReachable();
            _message = "ЗОЛОТО: РАДИУС. КРАСНЫЙ: ДОСТУПНАЯ ЦЕЛЬ. МЕЧ: АТАКА.";
            return;
        }
        if (capture is BattleCaptureStage.Arrow or BattleCaptureStage.Magic or
            BattleCaptureStage.ArrowImpact or BattleCaptureStage.MagicImpact)
        {
            var arrow = capture is BattleCaptureStage.Arrow or BattleCaptureStage.ArrowImpact;
            var impact = capture is BattleCaptureStage.ArrowImpact or BattleCaptureStage.MagicImpact;
            var attackerId = arrow ? "human.longbow" : "flora.ember-bloom";
            var attacker = Battle.Units.First(unit => unit.UnitTypeId == attackerId);
            var target = Battle.Units.First(unit => unit.Side != attacker.Side);
            BeginAttackAnimation(attacker, target);
            for (var frame = 0; frame < (impact ? 8 : 4); frame++) _attackEffect!.Advance(impact ? 0.075f : 0.08f);
            _actionSeconds = impact ? 0.2f : 0.48f;
            _message = arrow ? "ПРЕДПРОСМОТР: СТРЕЛА." : "ПРЕДПРОСМОТР: ПЛАМЕННЫЙ СНАРЯД.";
            return;
        }
        if (capture != BattleCaptureStage.Opening)
        {
            _hover = BattleCaptureSetup.Advance(Battle, capture);
            _healMode = capture == BattleCaptureStage.Healing;
            _message = _healMode ? "H: ЛЕЧЕНИЕ. ЛКМ ПО РАНЕНОМУ СОЮЗНИКУ ВОССТАНОВИТ 10 ОЗ." : "БОЙ ЗАВЕРШЁН.";
            RefreshReachable();
        }
    }

    public BattleScreen(PixelCanvas canvas, AssetCatalog assets, UnitCatalog catalog, CampaignBattle session,
        Func<EncounterResolution> returnToMap, Action exit, MouseState initialMouse, KeyboardState initialKeyboard)
        : this(canvas, assets, catalog, exit, false)
    {
        _seed = session.Encounter.Seed;
        _campaignBattle = session;
        _returnToMap = returnToMap;
        LoadBattle(session.Battle, session.Field);
        _previousMouse = initialMouse;
        _previousKeyboard = initialKeyboard;
    }

    private BattleScreen(PixelCanvas canvas, AssetCatalog assets, UnitCatalog catalog, Action exit, bool night)
    {
        _canvas = canvas;
        _assets = assets;
        _catalog = catalog;
        _font = new PixelFont(canvas);
        _hud = new BattleHud(canvas, assets, catalog);
        _exit = exit;
        _nightBlend = _nightTarget = night ? 1f : 0f;
    }

    private void Restart()
    {
        var demo = BattleDemo.Create(_catalog, _seed);
        LoadBattle(demo.Battle, demo.Field);
    }

    private void LoadBattle(Battle battle, GeneratedBattlefield field)
    {
        _interaction = new BattleInteraction(battle);
        _field = new BattlefieldRenderer(_canvas, _assets, field.Map, field.Props);
        _healMode = false;
        _hover = null;
        _opponentDelay = 0.7f;
        _actionUnitId = null;
        _actionTargetId = null;
        _actionSeconds = 0f;
        _attackEffect = null;
        _message = "ЛКМ: ПОКАЗАТЬ ПУТЬ. ПОВТОРНАЯ ЛКМ: ПЕРЕМЕСТИТЬ ОТРЯД.";
        RefreshReachable();
    }

    public void Update(MouseState mouse, KeyboardState keyboard, float elapsedSeconds)
    {
        _nightBlend = BattleLighting.AdvanceFade(_nightBlend, _nightTarget, elapsedSeconds);
        _currentMouse = mouse;
        _hover = mouse.Y < 600 ? _field.HitTest(mouse.Position) : null;
        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        if (Pressed(Keys.G) || clicked && BattleHud.GridButton.Contains(mouse.Position)) _showGrid = !_showGrid;
        if (Pressed(Keys.N)) _nightTarget = 1f - _nightTarget;

        if (_actionSeconds > 0f)
        {
            _actionSeconds = Math.Max(0f, _actionSeconds - Math.Clamp(elapsedSeconds, 0f, 0.1f));
            _attackEffect?.Advance(elapsedSeconds);
            _previousMouse = mouse;
            _previousKeyboard = keyboard;
            return;
        }

        if (Battle.IsFinished)
        {
            if (_returnToMap is not null)
            {
                if (Pressed(Keys.Enter) || Pressed(Keys.Escape) || clicked && ReturnButton.Contains(mouse.Position))
                {
                    var result = _returnToMap();
                    if (!result.Success) _resultError = "НЕ УДАЛОСЬ ПРИМЕНИТЬ ИТОГ. ПОВТОРИТЕ ВОЗВРАТ.";
                }
            }
            else if (Pressed(Keys.R) || clicked && RestartButton.Contains(mouse.Position)) Restart();
            else if (Pressed(Keys.Escape) || clicked && ExitButton.Contains(mouse.Position)) _exit();
        }
        else if (Pressed(Keys.Escape)) _exit();
        else if (_interaction.IsPlayerTurn)
        {
            _opponentDelay = 0.7f;
            if (Pressed(Keys.Enter) || clicked && BattleHud.EndTurnButton.Contains(mouse.Position))
            {
                _interaction.EndPlayerTurn();
                _healMode = false;
                _message = "ХОД ПЕРЕДАН СЛЕДУЮЩЕМУ ОТРЯДУ.";
                RefreshReachable();
            }
            else if (Pressed(Keys.H) && Battle.ActiveUnit is { Ability: UnitAbility.HealAlly } ||
                     Pressed(Keys.M) && Battle.ActiveUnit is { Ability: UnitAbility.RadiantModes } ||
                     clicked && BattleHud.HealButton.Contains(mouse.Position))
            {
                if (Battle.ActiveUnit is { Ability: UnitAbility.RadiantModes } inquisitor &&
                    !Battle.HasAttacked(inquisitor.Id))
                {
                    var nextMode = inquisitor.AttackMode == AttackMode.Hammer ? AttackMode.HolyFire : AttackMode.Hammer;
                    if (_interaction.TrySetAttackMode(nextMode))
                    {
                        _message = nextMode == AttackMode.HolyFire
                            ? $"ИНКВИЗИТОР: СВЯЩЕННЫЙ ОГОНЬ, ДАЛЬНОСТЬ {BattleRules.HolyFireRange}."
                            : "ИНКВИЗИТОР: МОЛОТ, УДАР ПО СОСЕДНЕЙ КЛЕТКЕ.";
                        RefreshReachable();
                    }
                }
                else if (Battle.ActiveUnit is { Ability: UnitAbility.HealAlly } active && !Battle.HasAttacked(active.Id))
                {
                    _healMode = !_healMode;
                    _message = _healMode ? "ЛЕЧЕНИЕ: ЛКМ ПО РАНЕНОМУ СОЮЗНИКУ. +10 ОЗ ДО НАЧАЛЬНОГО МАКСИМУМА."
                        : "ЛЕЧЕНИЕ ОТМЕНЕНО.";
                }
            }
            else if (clicked && _hover is { } coordinate)
                SelectCell(coordinate);
        }
        else
        {
            _opponentDelay -= Math.Clamp(elapsedSeconds, 0f, 0.1f);
            if (_opponentDelay <= 0)
            {
                var actor = Battle.ActiveUnit;
                var action = _interaction.StepOpponent();
                _opponentDelay = 0.7f;
                if (actor is not null && action?.Type == BattleActionType.Attack && Find(action.TargetId) is { } enemyTarget)
                    BeginAttackAnimation(actor, enemyTarget, conversion: enemyTarget.Side == actor.Side);
                if (actor is not null && action is not null)
                    _message = action.Type switch
                    {
                        BattleActionType.Attack => Name(actor) + ": АТАКА ПО " + Name(Find(action.TargetId)!),
                        BattleActionType.Move => Name(actor) + ": ПЕРЕМЕЩЕНИЕ.",
                        BattleActionType.SetAttackMode => Name(actor) + (action.Mode == AttackMode.HolyFire
                            ? ": СВЯЩЕННЫЙ ОГОНЬ." : ": МОЛОТ."),
                        _ => Name(actor) + ": ХОД ЗАВЕРШЁН."
                    };
                RefreshReachable();
            }
        }
        _previousMouse = mouse;
        _previousKeyboard = keyboard;
        return;

        bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void SelectCell(HexCoord coordinate)
    {
        var active = Battle.ActiveUnit!;
        var target = Battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Position == coordinate);
        if (_healMode)
        {
            var before = target?.RemainingHitPoints ?? 0;
            if (target is not null && _interaction.TryHeal(target.Id))
            {
                BeginAttackAnimation(active, target, healing: true);
                _message = $"{Name(target)}: ВОССТАНОВЛЕНО {target.RemainingHitPoints - before} ОЗ.";
                _healMode = false;
            }
            else _message = "НУЖЕН ЖИВОЙ РАНЕНЫЙ СОЮЗНИК. СЕБЯ ЛЕЧИТЬ НЕЛЬЗЯ.";
        }
        else if (target is not null && target.Side != active.Side)
        {
            var before = target.RemainingHitPoints;
            if (_interaction.TryAttack(target.Id))
            {
                BeginAttackAnimation(active, target, conversion: target.Side == active.Side);
                _message = $"{Name(active)}: УРОН {before - target.RemainingHitPoints}. " +
                    (target.Side == active.Side ? "ВРАГ НА НАШЕЙ СТОРОНЕ ДО КОНЦА БОЯ!" : $"В ЦЕЛИ ОСТАЛОСЬ {target.Count}.");
            }
            else _message = Battle.HasAttacked(active.Id) ? "ДЕЙСТВИЕ УЖЕ ИСПОЛЬЗОВАНО. ENTER: КОНЕЦ ХОДА."
                : "ЦЕЛЬ ВНЕ ДАЛЬНОСТИ ИЛИ ЛИНИЯ ВИДИМОСТИ ПЕРЕКРЫТА.";
        }
        else if (_interaction.SelectDestination(coordinate))
            _message = Name(active) + ": ПЕРЕМЕЩЕНИЕ ЗАВЕРШЕНО. МОЖНО АТАКОВАТЬ ИЛИ ПЕРЕДАТЬ ХОД.";
        else
            _message = Battle.HasMoved(active.Id) ? "ПЕРЕМЕЩЕНИЕ УЖЕ ИСПОЛЬЗОВАНО." :
                !_interaction.SelectedPath.IsReachable ? "ПУТЬ ПЕРЕКРЫТ ИЛИ КЛЕТКА ЗАНЯТА." :
                _interaction.SelectedPath.Cost > active.Movement ? "СЛИШКОМ ДАЛЕКО ДЛЯ ОДНОГО ХОДА." :
                $"ПУТЬ: {_interaction.SelectedPath.Cost} ИЗ {active.Movement}. ПОВТОРИТЕ ЛКМ ДЛЯ ХОДА.";
        RefreshReachable();
    }

    private void RefreshReachable()
    {
        _reachable.Clear();
        _attackArea.Clear();
        if (!_interaction.IsPlayerTurn || Battle.ActiveUnit is not { } active) return;
        if (!Battle.HasAttacked(active.Id))
            foreach (var tile in Battle.Map.Tiles)
                if (tile.IsPassable && BattleRules.IsWithinAttackRange(active, tile))
                    _attackArea.Add(tile.Coordinate);
        if (Battle.HasMoved(active.Id)) return;
        var occupied = Battle.Units.Where(unit => unit.IsAlive && unit.Id != active.Id).Select(unit => unit.Position).ToHashSet();
        foreach (var tile in Battle.Map.Tiles)
        {
            if (tile.Coordinate == active.Position || occupied.Contains(tile.Coordinate)) continue;
            var path = _pathfinder.FindPath(Battle.Map, active.Position, tile.Coordinate, occupied);
            if (path.IsReachable && path.Cost <= active.Movement) _reachable.Add(tile.Coordinate);
        }
    }

    private void BeginAttackAnimation(BattleUnit actor, BattleUnit target, bool healing = false,
        bool conversion = false)
    {
        _actionUnitId = actor.Id;
        _actionTargetId = target.Id;
        _actionSecondary = BattleAnimationSelector.UsesSecondaryClip(actor, healing, conversion);
        _actionSeconds = 0.8f;
        _attackEffect = new BattleAttackEffect(actor,
            _field.Layout.Center(Battle.Map.GetTile(actor.Position)!),
            _field.Layout.Center(Battle.Map.GetTile(target.Position)!), healing);
    }

    public void Draw()
    {
        _field.DrawTerrain(_showGrid, _nightBlend);
        DrawTacticalOverlay();
        _field.DrawProps(_nightBlend);
        foreach (var unit in Battle.Units.Where(unit => unit.IsAlive ||
                     _actionSeconds > 0f && unit.Id == _actionTargetId)
                     .OrderBy(unit => unit.Position.R).ThenBy(unit => unit.Position.Q))
            DrawUnit(unit);
        _attackEffect?.Draw(_canvas);
        DrawActionCursor();
        var hovered = _hover is { } cell ? Battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Position == cell) : null;
        _hud.Draw(Battle, Battle.IsFinished ? "БОЙ ЗАВЕРШЁН." : DescribeHover(hovered), hovered, _healMode, _showGrid);
        if (Battle.IsFinished && _actionSeconds <= 0f) DrawResult();
    }

    private void DrawTacticalOverlay()
    {
        if (_interaction.IsPlayerTurn && !_healMode)
        {
            if (Battle.ActiveUnit is { } active && !Battle.HasAttacked(active.Id))
                foreach (var coordinate in _attackArea)
                {
                    var tile = Battle.Map.GetTile(coordinate)!;
                    _canvas.Polygon(_field.Layout.Corners(tile, 5), Gold * 0.09f);
                    var corners = _field.Layout.Corners(tile, 4);
                    var neighbors = coordinate.GetNeighbors();
                    for (var edge = 0; edge < neighbors.Count; edge++)
                        if (!_attackArea.Contains(neighbors[edge]))
                        {
                            var side = EdgeByNeighbor[edge];
                            _canvas.Line(corners[side], corners[(side + 1) % 6], Gold * 0.74f, 2);
                        }
                }
            foreach (var coordinate in _reachable)
            {
                var corners = _field.Layout.Corners(Battle.Map.GetTile(coordinate)!, 5);
                _canvas.Polygon(corners, Green * 0.28f);
                _canvas.Outline(corners, Green * 0.9f, 2);
            }
        }
        if (!_healMode)
        {
            var path = _interaction.SelectedPath;
            var possible = Battle.ActiveUnit is { } active && !Battle.HasMoved(active.Id) && path.Cost <= active.Movement;
            for (var i = 1; i < path.Coordinates.Count; i++)
                _canvas.Line(_field.Layout.Center(Battle.Map.GetTile(path.Coordinates[i - 1])!),
                    _field.Layout.Center(Battle.Map.GetTile(path.Coordinates[i])!), possible ? Gold * 0.8f : Red * 0.6f, 2);
        }
        if (Battle.ActiveUnit is { } actor)
        {
            _canvas.Outline(_field.Layout.Corners(Battle.Map.GetTile(actor.Position)!, 6), Gold, 2);
            if (_interaction.IsPlayerTurn && !Battle.HasAttacked(actor.Id))
            foreach (var unit in Battle.Units.Where(unit => unit.IsAlive && unit.Id != actor.Id))
            {
                var healable = _healMode && unit.Side == actor.Side && unit.RemainingHitPoints < unit.MaximumHitPointsAtBattleStart;
                if (healable || !_healMode && BattleRules.CanAttack(actor, unit, Battle.Map))
                {
                    var corners = _field.Layout.Corners(Battle.Map.GetTile(unit.Position)!, 5);
                    _canvas.Polygon(corners, healable ? Blue * 0.23f : Red * 0.28f);
                    _canvas.Outline(corners, healable ? Blue : Red, 3);
                }
            }
        }
        if (_hover is { } hover)
            _canvas.Outline(_field.Layout.Corners(Battle.Map.GetTile(hover)!, 2),
                CursorColor(GetCursorKind(hover)), 3);
    }

    private enum CursorKind { Move, Attack, RangedAttack, Heal, InRange, Blocked }

    private CursorKind GetCursorKind(HexCoord coordinate)
    {
        if (!_interaction.IsPlayerTurn || Battle.ActiveUnit is not { } active) return CursorKind.Blocked;
        var occupant = Battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Position == coordinate);
        if (_healMode)
            return occupant is { } ally && ally.Id != active.Id && ally.Side == active.Side &&
                ally.RemainingHitPoints < ally.MaximumHitPointsAtBattleStart && !Battle.HasAttacked(active.Id)
                ? CursorKind.Heal : CursorKind.Blocked;
        if (occupant is not null)
        {
            if (occupant.Side == active.Side || Battle.HasAttacked(active.Id) ||
                !BattleRules.CanAttack(active, occupant, Battle.Map)) return CursorKind.Blocked;
            return active.IsRanged || active.Ability == UnitAbility.RadiantModes && active.AttackMode == AttackMode.HolyFire
                ? CursorKind.RangedAttack : CursorKind.Attack;
        }
        if (_reachable.Contains(coordinate)) return CursorKind.Move;
        var tile = Battle.Map.GetTile(coordinate);
        return tile is not null && _attackArea.Contains(coordinate) &&
            BattleRules.CanAttackTile(active, tile, Battle.Map) ? CursorKind.InRange : CursorKind.Blocked;
    }

    private static Color CursorColor(CursorKind kind) => kind switch
    {
        CursorKind.Move => Green,
        CursorKind.Attack or CursorKind.RangedAttack => Red,
        CursorKind.Heal => Blue,
        CursorKind.InRange => Gold,
        _ => new Color(120, 104, 91)
    };

    private void DrawActionCursor()
    {
        if (_hover is not { } coordinate || !_interaction.IsPlayerTurn) return;
        var kind = GetCursorKind(coordinate);
        var previewPoint = _field.Layout.Center(Battle.Map.GetTile(coordinate)!);
        var screenshotCursor = _currentMouse.X == 0 && _currentMouse.Y == 0;
        var mouseX = screenshotCursor ? (int)previewPoint.X + 8 : _currentMouse.X;
        var mouseY = screenshotCursor ? (int)previewPoint.Y - 24 : _currentMouse.Y;
        // Keep the action mark at the pointer hotspot. A displaced badge can sit over
        // a neighboring hex and show a different action than the cell the click uses.
        var x = Math.Clamp(mouseX + 2, 4, 1246);
        var y = Math.Clamp(mouseY + 2, 4, 570);
        var color = CursorColor(kind);
        _canvas.Rect(x, y, 26, 25, new Color(39, 32, 23) * 0.9f);
        _canvas.Frame(x, y, 26, 25, color, 2);
        var a = new Vector2(x + 6, y + 6);
        var b = new Vector2(x + 19, y + 18);
        if (kind == CursorKind.Move)
        {
            _canvas.Line(new Vector2(x + 6, y + 18), new Vector2(x + 19, y + 7), color, 3);
            _canvas.Line(new Vector2(x + 12, y + 7), new Vector2(x + 19, y + 7), color, 3);
            _canvas.Line(new Vector2(x + 19, y + 7), new Vector2(x + 19, y + 14), color, 3);
        }
        else if (kind == CursorKind.Heal)
        {
            _canvas.Rect(x + 11, y + 5, 4, 15, color);
            _canvas.Rect(x + 6, y + 10, 14, 4, color);
        }
        else if (kind == CursorKind.RangedAttack)
        {
            _canvas.Line(new Vector2(x + 5, y + 20), new Vector2(x + 20, y + 5), color, 3);
            _canvas.Line(new Vector2(x + 11, y + 5), new Vector2(x + 20, y + 5), color, 2);
            _canvas.Line(new Vector2(x + 20, y + 5), new Vector2(x + 20, y + 14), color, 2);
            _canvas.Line(new Vector2(x + 5, y + 13), new Vector2(x + 12, y + 20), Gold, 2);
        }
        else if (kind == CursorKind.Attack)
        {
            _canvas.Line(new Vector2(x + 5, y + 20), new Vector2(x + 20, y + 5), color, 3);
            _canvas.Line(new Vector2(x + 6, y + 12), new Vector2(x + 13, y + 19), Gold, 2);
            _canvas.Rect(x + 3, y + 18, 5, 4, Gold);
        }
        else if (kind == CursorKind.InRange)
        {
            _canvas.Frame(x + 8, y + 7, 10, 10, color, 2);
            _canvas.Line(new Vector2(x + 13, y + 4), new Vector2(x + 13, y + 20), color, 1);
            _canvas.Line(new Vector2(x + 5, y + 12), new Vector2(x + 21, y + 12), color, 1);
        }
        else
        {
            _canvas.Line(a, b, color, 2);
            _canvas.Line(new Vector2(x + 19, y + 6), new Vector2(x + 6, y + 18), color, 2);
        }
    }

    private void DrawUnit(BattleUnit unit)
    {
        var point = _field.Layout.Center(Battle.Map.GetTile(unit.Position)!);
        var x = (int)point.X;
        var y = (int)point.Y;
        var ring = Enumerable.Range(0, 20).Select(i => new Vector2(x + MathF.Cos(i * MathHelper.TwoPi / 20) * 25,
            y + 16 + MathF.Sin(i * MathHelper.TwoPi / 20) * 7)).ToArray();
        _canvas.Polygon(ring, new Color(24, 26, 19) * 0.65f);
        _canvas.Outline(ring, unit.Side == BattleSide.Player ? Green * 0.8f : Red * 0.8f, 1);
        using (_canvas.PushTint(BattleLighting.ForegroundTint(_nightBlend)))
        {
            var key = _catalog.Get(unit.UnitTypeId)!.ArtKey;
            var (spriteWidth, spriteHeight) = unit.UnitTypeId switch
            {
                "human.inquisitor" => (88, 100),
                "human.banner-knight" => (83, 94),
                "flora.root-warden" => (75, 88),
                _ => (56, 66)
            };
            var bounds = new Rectangle(x - spriteWidth / 2, y + 17 - spriteHeight,
                spriteWidth, spriteHeight);
            var actionFrame = _actionSeconds > 0f && _actionUnitId == unit.Id
                ? Math.Min(7, (int)((0.8f - _actionSeconds) / 0.1f)) : -1;
            var humanAction = actionFrame >= 2 && actionFrame <= 5 &&
                unit.UnitTypeId.StartsWith("human.", StringComparison.Ordinal) &&
                _assets.HasAsset(key + ".attack");
            if (actionFrame >= 0 && unit.UnitTypeId.StartsWith("human.", StringComparison.Ordinal))
            {
                var recoil = actionFrame switch { 0 or 7 => 0, 1 or 6 => 1, 2 or 5 => 2, _ => 3 };
                bounds.Offset(unit.Side == BattleSide.Enemy ? -recoil : recoil, 0);
            }
            var animating = actionFrame >= 0 && !unit.UnitTypeId.StartsWith("human.", StringComparison.Ordinal) &&
                _assets.TryDrawAction(_canvas, "unit." + unit.UnitTypeId + ".actions",
                    ActionSpriteBounds(x, y + 17, spriteWidth, spriteHeight),
                    actionFrame, _actionSecondary,
                    unit.Side == BattleSide.Enemy);
            var drawKey = key;
            if (humanAction) drawKey = key + ".attack";
            if (humanAction && unit.UnitTypeId == "human.longbow" && actionFrame >= 3 &&
                _assets.HasAsset(key + ".released"))
                drawKey = key + ".released";
            var drawBounds = humanAction
                ? new Rectangle(bounds.X - 11, bounds.Y, bounds.Width + 22, bounds.Height) : bounds;
            if (!animating && !_assets.TryDrawCropped(_canvas, drawKey, drawBounds, unit.Side == BattleSide.Enemy) &&
                !_assets.TryDraw(_canvas, AssetCatalog.HumanGuard, new Rectangle(x - 24, y - 42, 48, 64)))
                FantasyArt.DrawGuard(_canvas, x, y);
        }
        if (!unit.IsAlive) return; // Keep a defeated target visible until the projectile lands.
        var label = unit.UnitTypeId switch
        {
            "human.militia" => "О " + unit.Count,
            "human.banner-knight" => "Р " + unit.Count,
            "human.longbow" => "Л " + unit.Count,
            _ => unit.Count.ToString()
        };
        var width = label.Length * 6 + 8;
        _canvas.Rect(x + 12, y + 12, width, 13, new Color(35, 29, 23));
        _canvas.Frame(x + 12, y + 12, width, 13, unit.Side == BattleSide.Player ? Green : Red, 1);
        _font.Draw(label, x + 16, y + 15, Gold, 1);
    }

    private static Rectangle ActionSpriteBounds(int centerX, int groundY, int width, int height)
    {
        var scaledWidth = (int)MathF.Round(width * ActionSpriteScale);
        var scaledHeight = (int)MathF.Round(height * ActionSpriteScale);
        var anchorY = (int)MathF.Round(scaledHeight * 72f / 80f);
        return new Rectangle(centerX - scaledWidth / 2, groundY - anchorY, scaledWidth, scaledHeight);
    }

    private string DescribeHover(BattleUnit? unit)
    {
        if (Battle.IsFinished || !_interaction.IsPlayerTurn) return _message;
        if (unit is not null && Battle.ActiveUnit is { } active && unit.Id != active.Id)
        {
            if (_healMode) return unit.Side == active.Side ? "ЛКМ: ВОССТАНОВИТЬ 10 ОЗ В ПРЕДЕЛАХ НАЧАЛЬНОГО ЗДОРОВЬЯ."
                : "ВРАЖЕСКИЙ ОТРЯД НЕЛЬЗЯ ЛЕЧИТЬ.";
            if (unit.Side != active.Side)
            {
                if (Battle.HasAttacked(active.Id)) return "ДЕЙСТВИЕ УЖЕ ИСПОЛЬЗОВАНО.";
                var reason = BattleRules.GetAttackFailureReason(active, unit, Battle.Map);
                if (reason == AttackFailureReason.None)
                {
                    var ranged = active.IsRanged || active.Ability == UnitAbility.RadiantModes &&
                        active.AttackMode == AttackMode.HolyFire;
                    return (ranged ? "ЛКМ: ВЫСТРЕЛИТЬ. " : "ЛКМ: АТАКОВАТЬ. ") +
                           "УРОН " + (long)active.Count * active.DamagePerCreature;
                }

                return reason switch
                {
                    AttackFailureReason.OutOfRange => $"ВНЕ ДАЛЬНОСТИ: {active.Position.DistanceTo(unit.Position)} КЛЕТОК. " +
                        $"РАДИУС {BattleRules.GetEffectiveRange(active, Battle.Map.GetTile(unit.Position)!)}.",
                    AttackFailureReason.BlockedLineOfSight => "ВЫСТРЕЛ ПЕРЕКРЫТ: МЕЖДУ ОТРЯДАМИ ПРЕПЯТСТВИЕ ИЛИ ВЫСОТА.",
                    AttackFailureReason.FriendlyTarget => "НЕЛЬЗЯ АТАКОВАТЬ СОЮЗНИКА.",
                    _ => "ЦЕЛЬ НЕ МОЖЕТ БЫТЬ АТАКОВАНА."
                };
            }
        }
        if (_hover is { } coordinate && Battle.ActiveUnit is { } actor)
        {
            var tile = Battle.Map.GetTile(coordinate)!;
            var ranged = actor.IsRanged || actor.Ability == UnitAbility.RadiantModes && actor.AttackMode == AttackMode.HolyFire;
            if (ranged && !Battle.HasAttacked(actor.Id) &&
                BattleRules.IsRangedShotBlocked(actor, tile, Battle.Map))
                return "ДАЛЬНОСТЬ ДОСТАТОЧНА, НО ЛИНИЯ ВЫСТРЕЛА ПЕРЕКРЫТА.";
            var height = tile.Height;
            var delta = height - actor.Height;
            if (delta != 0 && unit is null)
            {
                var enemies = Battle.GetEnemies(actor.Side).Select(target => target.Height);
                var preview = ElevationChangePreview.Between(actor.Height, height, enemies);
                return $"ВЫСОТА {height} ({delta:+0;-0}). " + (!ranged ? "БОНУС ДАЛЬНОСТИ ТОЛЬКО У СТРЕЛКОВ." :
                    $"+2 ДАЛЬНОСТИ: ПОТЕРЯ У {preview.LostRangeBonusTargetCount}, ПОЛУЧЕНИЕ У {preview.GainedRangeBonusTargetCount} ЦЕЛЕЙ.");
            }
        }
        return _message;
    }

    private void DrawResult()
    {
        _canvas.Rect(0, 0, 1280, 610, Color.Black * 0.6f);
        _canvas.Rect(368, 164, 544, 386, new Color(44, 36, 25));
        _canvas.Frame(368, 164, 544, 386, Gold, 2);
        var title = Battle.Winner == BattleSide.Player ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
        _font.Draw(title, 640 - title.Length * 12, 190, Gold, 4);
        _font.Draw("ОТРЯД", 402, 244, Gold, 1);
        _font.Draw("ВЫЖИЛО / ПОГИБЛО", 732, 244, Gold, 1);
        var index = 0;
        foreach (var unit in Battle.Units.Where(unit => unit.OriginalSide == BattleSide.Player))
        {
            var initial = unit.MaximumHitPointsAtBattleStart / unit.HitPointsPerCreature;
            _font.Draw(Name(unit), 402, 274 + index * 30, new Color(221, 211, 174), 2);
            _font.Draw($"{unit.Count} / {initial - unit.Count}", 732, 274 + index++ * 30, Gold, 2);
        }
        if (_campaignBattle is not null)
        {
            var encounter = _campaignBattle.Encounter;
            _font.Draw(_resultError ?? (Battle.Winner == BattleSide.Player
                ? $"НАГРАДА: {encounter.GoldReward} ЗОЛОТА, {encounter.ExperienceReward} ОПЫТА."
                : "ПОРАЖЕНИЕ: НАГРАДЫ НЕТ. ПОТЕРИ СОХРАНЯТСЯ."), 402, 444, Gold, 1);
            _font.Draw("ВЫЖИВШИЕ ВЕРНУТСЯ В АРМИЮ ПОЛНОСТЬЮ ЗДОРОВЫМИ.", 402, 461, Gold, 1);
            ResultButton(ReturnButton, "ENTER / ESC: НА КАРТУ");
        }
        else
        {
            _font.Draw("ПРОБНЫЙ БОЙ: АРМИЯ КАМПАНИИ НЕ ИЗМЕНЯЕТСЯ.", 402, 444, Gold, 1);
            ResultButton(RestartButton, "R: ПОВТОРИТЬ");
            ResultButton(ExitButton, "ESC: ВЫХОД");
        }
    }

    private void ResultButton(Rectangle rect, string text)
    {
        _canvas.Frame(rect.X, rect.Y, rect.Width, rect.Height, Gold, 1);
        _font.Draw(text, rect.X + (rect.Width - text.Length * 6) / 2, rect.Y + 13, Gold, 1);
    }

    private BattleUnit? Find(string? id) => Battle.Units.FirstOrDefault(unit => unit.Id == id);
    private string Name(BattleUnit unit) => _catalog.Get(unit.UnitTypeId)?.Name ?? unit.UnitTypeId;
}
