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
    private bool CanIssueCommands => BattleHud.CanIssueCommands(Battle, _actionSeconds > 0f);
    private static readonly Color Gold = new(218, 190, 113);
    private static readonly Color Green = new(149, 190, 107);
    private static readonly Color Red = new(210, 118, 89);
    private static readonly Color Blue = new(125, 195, 185);
    private static readonly int[] EdgeByNeighbor = [1, 0, 5, 4, 3, 2];
    private static readonly Rectangle RestartButton = new(444, 488, 186, 34);
    private static readonly Rectangle ExitButton = new(650, 488, 186, 34);
    private static readonly Rectangle ReturnButton = new(517, 488, 246, 34);

    public BattleScreen(PixelCanvas canvas, AssetCatalog assets, UnitCatalog catalog,
        int seed, bool night, Action exit, BattleCaptureStage capture = BattleCaptureStage.Opening)
        : this(canvas, assets, catalog, exit, night)
    {
        _seed = seed;
        if (capture == BattleCaptureStage.Undead)
        {
            var demo = BattleDemo.Create(_catalog, _seed, undeadPreview: true);
            LoadBattle(demo.Battle, demo.Field);
            return;
        }
        if (capture == BattleCaptureStage.Outpost)
        {
            var demo = BattleDemo.Create(_catalog, _seed, mountain: true);
            LoadBattle(demo.Battle, demo.Field);
            return;
        }
        if (capture == BattleCaptureStage.HolyFire)
        {
            var demo = BattleDemo.Create(_catalog, _seed, inquisitorPreview: true);
            LoadBattle(demo.Battle, demo.Field);
            var caster = Battle.Units.First(unit => unit.UnitTypeId == "human.inquisitor");
            for (var step = 0; step < Battle.Units.Count && Battle.ActiveUnit != caster; step++)
                Battle.EndActivation();
            Battle.TrySetAttackMode(caster.Id, AttackMode.HolyFire);
            RefreshReachable();
            var target = Battle.Units.First(unit => unit.Side != caster.Side);
            BeginAttackAnimation(caster, target);
            for (var frame = 0; frame < 4; frame++) _attackEffect!.Advance(0.08f);
            _actionSeconds = 0.48f;
            _message = "ПРЕДПРОСМОТР: СВЯЩЕННЫЙ ОГОНЬ ИНКВИЗИТОРА.";
            return;
        }
        if (capture == BattleCaptureStage.Initiative)
        {
            var demo = BattleDemo.Create(_catalog, _seed, fullRoster: true);
            LoadBattle(demo.Battle, demo.Field);
            BattleCaptureSetup.Advance(Battle, capture);
            RefreshReachable();
            _message = "ОЧЕРЕДЬ: ТЕКУЩИЙ ОТРЯД СЛЕВА. ПОХОДИВШИЕ УБРАНЫ ДО НОВОГО РАУНДА.";
            return;
        }
        if (capture is BattleCaptureStage.HunterAttack or BattleCaptureStage.HunterRest)
        {
            var demo = BattleDemo.Create(_catalog, _seed, fullRoster: true);
            LoadBattle(demo.Battle, demo.Field);
            var cell = BattleCaptureSetup.Advance(Battle, capture);
            RefreshReachable();
            if (capture == BattleCaptureStage.HunterAttack)
            {
                var actor = Battle.ActiveUnit!;
                var target = Battle.Units.Single(unit => unit.IsAlive && unit.Position == cell);
                if (!Battle.Attack(actor.Id, target.Id))
                    throw new InvalidOperationException("Cannot execute the hunter capture attack.");
                BeginAttackAnimation(actor, target);
                for (var frame = 0; frame < 4; frame++) _attackEffect!.Advance(0.08f);
                _actionSeconds = 0.48f;
            }
            _message = capture == BattleCaptureStage.HunterAttack
                ? "ЛОВЧИЙ: ПРИСЕДАЮЩАЯ АТАКА СОХРАНЯЕТ МАСШТАБ ТЕЛА." : "ЛОВЧИЙ: ПОЗА ПЕРЕД ДОСТУПНОЙ АТАКОЙ.";
            return;
        }
        Restart();
        if (capture is BattleCaptureStage.Range or BattleCaptureStage.BlockedShot)
        {
            for (var step = 0; step < Battle.Units.Count * 2 &&
                 Battle.ActiveUnit?.UnitTypeId != "flora.sprout"; step++)
            {
                Battle.EndActivation();
                if (Battle.ActiveUnit is null) Battle.StartRound();
            }
            RefreshReachable();
            _hover = _attackArea.Where(cell => !_reachable.Contains(cell) &&
                    Battle.Units.All(unit => unit.Position != cell) &&
                    (capture != BattleCaptureStage.BlockedShot ||
                     !BattleRules.CanAttackTile(Battle.ActiveUnit!, Battle.Map.GetTile(cell)!, Battle.Map)))
                .OrderBy(cell => cell.DistanceTo(Battle.ActiveUnit!.Position))
                .LastOrDefault();
            _message = "ЗЕЛЁНЫЙ: ХОД. СВЕТЛОЕ ЗОЛОТО С ТЁМНОЙ ОБВОДКОЙ: ДАЛЬНОСТЬ. КРАСНЫЙ: ЦЕЛЬ.";
            return;
        }
        if (capture is BattleCaptureStage.Target or BattleCaptureStage.AfterAttack or BattleCaptureStage.ActionLock)
        {
            _hover = BattleCaptureSetup.Advance(Battle,
                capture == BattleCaptureStage.ActionLock ? BattleCaptureStage.Target : capture);
            RefreshReachable();
            if (capture == BattleCaptureStage.ActionLock)
            {
                var actor = Battle.ActiveUnit!;
                var target = Battle.Units.Single(unit => unit.IsAlive && unit.Position == _hover);
                if (!Battle.Attack(actor.Id, target.Id))
                    throw new InvalidOperationException("Cannot execute the action-lock capture attack.");
                BeginAttackAnimation(actor, target);
                for (var frame = 0; frame < 4; frame++) _attackEffect!.Advance(0.1f);
                _actionSeconds = 0.4f;
                RefreshReachable();
                return;
            }
            _message = "КРАСНЫЙ КОНТУР: ЦЕЛЬ МОЖНО АТАКОВАТЬ СЕЙЧАС.";
            if (capture == BattleCaptureStage.AfterAttack)
            {
                _hover = _reachable.OrderBy(cell => cell.DistanceTo(Battle.ActiveUnit!.Position)).FirstOrDefault();
                _message = "АТАКА ИСПОЛЬЗОВАНА. ЗЕЛЁНЫЕ КЛЕТКИ: МОЖНО ЕЩЁ ПЕРЕМЕСТИТЬСЯ.";
            }
            return;
        }
        if (capture is BattleCaptureStage.RangedTarget or BattleCaptureStage.SpriteTarget)
        {
            _hover = BattleCaptureSetup.Advance(Battle, BattleCaptureStage.RangedTarget);
            RefreshReachable();
            _message = "ЗОЛОТО: РАДИУС. КРАСНЫЙ: ДОСТУПНАЯ ЦЕЛЬ. МЕЧ: АТАКА.";
            if (capture == BattleCaptureStage.SpriteTarget) CaptureSpriteTarget();
            return;
        }
        if (capture is BattleCaptureStage.Arrow or BattleCaptureStage.Magic or
            BattleCaptureStage.ArrowImpact or BattleCaptureStage.MagicImpact or BattleCaptureStage.Melee)
        {
            if (capture == BattleCaptureStage.Melee)
            {
                var meleeActor = Battle.Units.First(unit => unit.UnitTypeId == "flora.thorn-guard");
                var meleeTarget = Battle.Units.First(unit => unit.Side != meleeActor.Side);
                for (var step = 0; step < Battle.Units.Count && Battle.ActiveUnit != meleeActor; step++)
                    Battle.EndActivation();
                RefreshReachable();
                BeginAttackAnimation(meleeActor, meleeTarget);
                for (var frame = 0; frame < 4; frame++) _attackEffect!.Advance(0.1f);
                _actionSeconds = 0.4f;
                _message = "ПРЕДПРОСМОТР: УДАР КАБАЧКА БЕЗ СНАРЯДА.";
                return;
            }
            var arrow = capture is BattleCaptureStage.Arrow or BattleCaptureStage.ArrowImpact;
            var impact = capture is BattleCaptureStage.ArrowImpact or BattleCaptureStage.MagicImpact;
            var attackerId = arrow ? "human.longbow" : "flora.ember-bloom";
            var attacker = Battle.Units.First(unit => unit.UnitTypeId == attackerId);
            var target = Battle.Units.First(unit => unit.Side != attacker.Side);
            for (var step = 0; step < Battle.Units.Count && Battle.ActiveUnit != attacker; step++)
                Battle.EndActivation();
            RefreshReachable();
            BeginAttackAnimation(attacker, target);
            for (var frame = 0; frame < (impact ? 8 : 4); frame++) _attackEffect!.Advance(impact ? 0.075f : 0.08f);
            _actionSeconds = AttackAnimationTiming.Duration - (impact ? 0.6f : 0.32f);
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
        : this(canvas, assets, catalog, exit, session.IsNight)
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
        _field = new BattlefieldRenderer(_canvas, _assets, field.Map, field.Props, field.IsMountainOutpost);
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
        _hover = mouse.Y < 600 && mouse.Y >= 0 ? HitTestBattle(mouse.Position) : null;
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
                if (actor is not null && action?.Type == BattleActionType.Heal && Find(action.TargetId) is { } healed)
                    BeginAttackAnimation(actor, healed, healing: true);
                if (actor is not null && action is not null)
                    _message = action.Type switch
                    {
                        BattleActionType.Attack => Name(actor) + ": АТАКА ПО " + Name(Find(action.TargetId)!),
                        BattleActionType.Heal => Name(actor) + ": ЛЕЧЕНИЕ " + Name(Find(action.TargetId)!),
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
        _actionSeconds = AttackAnimationTiming.Duration;
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
        var hovered = _actionSeconds <= 0f && _hover is { } cell
            ? Battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Position == cell) : null;
        _hud.Draw(Battle, _actionSeconds > 0f ? "ДОЖДИТЕСЬ ЗАВЕРШЕНИЯ ДЕЙСТВИЯ."
            : Battle.IsFinished ? "БОЙ ЗАВЕРШЁН." : DescribeHover(hovered), hovered, _healMode, _showGrid,
            actionInProgress: _actionSeconds > 0f);
        if (Battle.IsFinished && _actionSeconds <= 0f) DrawResult();
    }

    private void DrawTacticalOverlay()
    {
        if (CanIssueCommands && !_healMode)
        {
            // Moving and attacking are independent allowances: an attack does not consume movement.
            foreach (var coordinate in _reachable)
            {
                var corners = _field.Layout.Corners(Battle.Map.GetTile(coordinate)!, 5);
                _canvas.Polygon(corners, Green * 0.28f);
                _canvas.Outline(corners, Green * 0.9f, 2);
            }
            if (Battle.ActiveUnit is { } active && !Battle.HasAttacked(active.Id))
            {
                foreach (var coordinate in _attackArea)
                {
                    var tile = Battle.Map.GetTile(coordinate)!;
                    var corners = _field.Layout.Corners(tile, 4);
                    if (!BattleRules.CanAttackTile(active, tile, Battle.Map))
                    {
                        if (_reachable.Contains(coordinate)) continue;
                        // Within distance, but the shot is blocked: no bright attack fill.
                        var center = _field.Layout.Center(tile);
                        var muted = new Color(143, 118, 77);
                        _canvas.Outline(corners, muted * 0.65f, 1);
                        _canvas.Line(center + new Vector2(-7, -6), center + new Vector2(7, 6), muted, 2);
                        _canvas.Line(center + new Vector2(7, -6), center + new Vector2(-7, 6), muted, 2);
                        continue;
                    }
                    _canvas.Polygon(corners, new Color(218, 150, 45) * 0.23f);
                    _canvas.Outline(corners, new Color(34, 27, 20) * 0.82f, 3);
                    _canvas.Outline(corners, new Color(255, 226, 142) * 0.96f, 1);
                }
            }
        }
        if (CanIssueCommands && !_healMode)
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
            if (CanIssueCommands && !Battle.HasAttacked(actor.Id))
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
        if (_actionSeconds <= 0f && _hover is { } hover)
            _canvas.Outline(_field.Layout.Corners(Battle.Map.GetTile(hover)!, 2),
                CursorColor(GetCursorKind(hover)), 3);
    }

    private BattleCellAction GetCursorKind(HexCoord coordinate) => _interaction.GetCellAction(coordinate, _healMode);

    private static Color CursorColor(BattleCellAction kind) => kind switch
    {
        BattleCellAction.Move => Green,
        BattleCellAction.Attack or BattleCellAction.RangedAttack => Red,
        BattleCellAction.Heal => Blue,
        BattleCellAction.InRange => Gold,
        _ => new Color(120, 104, 91)
    };

    private void DrawActionCursor()
    {
        if (_hover is not { } coordinate || !CanIssueCommands) return;
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
        if (kind == BattleCellAction.Move)
        {
            _canvas.Line(new Vector2(x + 6, y + 18), new Vector2(x + 19, y + 7), color, 3);
            _canvas.Line(new Vector2(x + 12, y + 7), new Vector2(x + 19, y + 7), color, 3);
            _canvas.Line(new Vector2(x + 19, y + 7), new Vector2(x + 19, y + 14), color, 3);
        }
        else if (kind == BattleCellAction.Heal)
        {
            _canvas.Rect(x + 11, y + 5, 4, 15, color);
            _canvas.Rect(x + 6, y + 10, 14, 4, color);
        }
        else if (kind == BattleCellAction.RangedAttack)
        {
            _canvas.Line(new Vector2(x + 5, y + 20), new Vector2(x + 20, y + 5), color, 3);
            _canvas.Line(new Vector2(x + 11, y + 5), new Vector2(x + 20, y + 5), color, 2);
            _canvas.Line(new Vector2(x + 20, y + 5), new Vector2(x + 20, y + 14), color, 2);
            _canvas.Line(new Vector2(x + 5, y + 13), new Vector2(x + 12, y + 20), Gold, 2);
        }
        else if (kind == BattleCellAction.Attack)
        {
            _canvas.Line(new Vector2(x + 5, y + 20), new Vector2(x + 20, y + 5), color, 3);
            _canvas.Line(new Vector2(x + 6, y + 12), new Vector2(x + 13, y + 19), Gold, 2);
            _canvas.Rect(x + 3, y + 18, 5, 4, Gold);
        }
        else if (kind == BattleCellAction.InRange)
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
        var shadowKey = _catalog.Get(unit.UnitTypeId)!.ArtKey;
        ContactShadow.Draw(_canvas, _assets.GetCroppedDestination(shadowKey,
            BattleUnitSpriteLayout.Bounds(unit.UnitTypeId, new Point(x, y))));
        _canvas.Outline(ring, unit.Side == BattleSide.Player ? Green * 0.8f : Red * 0.8f, 1);
        using (_canvas.PushTint(BattleLighting.ForegroundTint(_nightBlend)))
        {
            var key = _catalog.Get(unit.UnitTypeId)!.ArtKey;
            var bounds = BattleUnitSpriteLayout.Bounds(unit.UnitTypeId, new Point(x, y));
            var actionFrame = _actionSeconds > 0f && _actionUnitId == unit.Id
                ? AttackAnimationTiming.FrameAt(_actionSeconds) : -1;
            var actionKey = BattleAnimationSelector.HumanPoseKey(key, _actionSecondary);
            var humanAction = actionFrame >= 1 && actionFrame <= 5 &&
                unit.UnitTypeId.StartsWith("human.", StringComparison.Ordinal) &&
                _assets.HasAsset(actionKey);
            if (actionFrame >= 0 && unit.UnitTypeId.StartsWith("human.", StringComparison.Ordinal))
            {
                var recoil = actionFrame switch { 0 or 7 => 0, 1 or 6 => 1, 2 or 5 => 2, _ => 3 };
                bounds.Offset(unit.Side == BattleSide.Enemy ? -recoil : recoil, 0);
            }
            var animating = actionFrame >= 0 && !unit.UnitTypeId.StartsWith("human.", StringComparison.Ordinal) &&
                _assets.TryDrawAction(_canvas, "unit." + unit.UnitTypeId + ".actions",
                    _assets.GetCroppedDestination(key, bounds),
                    actionFrame, _actionSecondary,
                    unit.Side == BattleSide.Enemy);
            var drawKey = key;
            if (humanAction) drawKey = actionKey;
            if (humanAction && unit.UnitTypeId == "human.longbow" && actionFrame >= AttackAnimationTiming.ReleaseFrame &&
                _assets.HasAsset(key + ".released"))
                drawKey = key + ".released";
            var drawBounds = humanAction ? BattleUnitSpriteLayout.ActionBounds(unit.UnitTypeId, bounds) : bounds;
            if (!animating && !_assets.TryDrawCropped(_canvas, drawKey, drawBounds, unit.Side == BattleSide.Enemy) &&
                !_assets.TryDraw(_canvas, AssetCatalog.HumanGuard, new Rectangle(x - 24, y - 42, 48, 64)))
                FantasyArt.DrawGuard(_canvas, x, y);
        }
        if (!unit.IsAlive) return; // Keep a defeated target visible until the projectile lands.
        var label = BattleUnitSpriteLayout.CountLabel(unit.UnitTypeId, unit.Count);
        var badge = BattleUnitSpriteLayout.CountBounds(label, new Point(x, y));
        _canvas.Rect(badge.X, badge.Y, badge.Width, badge.Height, new Color(35, 29, 23));
        _canvas.Frame(badge.X, badge.Y, badge.Width, badge.Height, unit.Side == BattleSide.Player ? Green : Red, 1);
        _font.Draw(label, x + 16, y + 15, Gold, 1);
    }

    private HexCoord? HitTestBattle(Point pointer)
    {
        // Commands are locked during action poses. Resting sprites are the selectable shapes.
        if (_actionSeconds <= 0)
        foreach (var unit in Battle.Units.Where(unit => unit.IsAlive)
                     .OrderByDescending(unit => unit.Position.R).ThenByDescending(unit => unit.Position.Q))
        {
            var center = _field.Layout.Center(Battle.Map.GetTile(unit.Position)!);
            var anchor = new Point((int)center.X, (int)center.Y);
            var bounds = BattleUnitSpriteLayout.Bounds(unit.UnitTypeId, anchor);
            var label = BattleUnitSpriteLayout.CountLabel(unit.UnitTypeId, unit.Count);
            if (BattleUnitSpriteLayout.CountBounds(label, anchor).Contains(pointer)) return unit.Position;
            var key = _catalog.Get(unit.UnitTypeId)!.ArtKey;
            if (_assets.IsCroppedPixelHit(key, bounds, pointer, unit.Side == BattleSide.Enemy))
                return unit.Position;
        }
        return _field.HitTest(pointer);
    }

    private void CaptureSpriteTarget()
    {
        var target = Battle.Units.First(unit => unit.IsAlive && unit.Position == _hover);
        var center = _field.Layout.Center(Battle.Map.GetTile(target.Position)!);
        var bounds = BattleUnitSpriteLayout.Bounds(target.UnitTypeId, new Point((int)center.X, (int)center.Y));
        var key = _catalog.Get(target.UnitTypeId)!.ArtKey;
        // Pick a genuinely visible pixel over another ground hex: the old input selected that hex.
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        foreach (var x in Enumerable.Range(bounds.Left, bounds.Width).OrderBy(x => Math.Abs(x - bounds.Center.X)))
        {
            var point = new Point(x, y);
            if (_field.HitTest(point) == target.Position ||
                !_assets.IsCroppedPixelHit(key, bounds, point, target.Side == BattleSide.Enemy) ||
                HitTestBattle(point) != target.Position) continue;
            _currentMouse = new MouseState(x, y, 0, ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _hover = HitTestBattle(point);
            _message = "НАВЕДЕНИЕ НА ВЕРХ МОДЕЛИ ВЫБИРАЕТ ЕЁ ОТРЯД, А НЕ СОСЕДНЮЮ КЛЕТКУ.";
            return;
        }
        throw new InvalidOperationException("No visible sprite pixel outside its ground hex in the capture fixture.");
    }

    private string DescribeHover(BattleUnit? unit)
    {
        if (Battle.IsFinished || !_interaction.IsPlayerTurn) return _message;
        if (_healMode && Battle.ActiveUnit is { } healer)
        {
            if (unit is null) return "ЛЕЧЕНИЕ: ВЫБЕРИТЕ РАНЕНОГО СОЮЗНИКА.";
            if (unit.Id == healer.Id) return "СЕБЯ ЛЕЧИТЬ НЕЛЬЗЯ.";
            if (unit.Side != healer.Side) return "ВРАЖЕСКИЙ ОТРЯД НЕЛЬЗЯ ЛЕЧИТЬ.";
            return _interaction.GetCellAction(unit.Position, healing: true) == BattleCellAction.Heal
                ? "ЛКМ: ВОССТАНОВИТЬ 10 ОЗ В ПРЕДЕЛАХ НАЧАЛЬНОГО ЗДОРОВЬЯ."
                : "ОТРЯД НЕ НУЖДАЕТСЯ В ЛЕЧЕНИИ ИЛИ ДЕЙСТВИЕ УЖЕ ИСПОЛЬЗОВАНО.";
        }
        if (unit is not null && Battle.ActiveUnit is { } active && unit.Id != active.Id)
        {
            if (unit.Side != active.Side)
            {
                if (Battle.HasAttacked(active.Id)) return "ДЕЙСТВИЕ УЖЕ ИСПОЛЬЗОВАНО.";
                var reason = BattleRules.GetAttackFailureReason(active, unit, Battle.Map);
                if (reason == AttackFailureReason.None)
                {
                    var ranged = active.IsRanged || active.Ability == UnitAbility.RadiantModes &&
                        active.AttackMode == AttackMode.HolyFire;
                    return (ranged ? "ЛКМ: ВЫСТРЕЛИТЬ. " : "ЛКМ: АТАКОВАТЬ. ") +
                           "УРОН " + BattleRules.GetAttackDamage(active, unit);
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
