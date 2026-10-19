using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Combat.Pathfinding;
using Game.Core.Content;
using Game.Core.Geometry;
using Game.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame.Screens;

/// <summary>Playable sandbox battle. Rules and turn ownership remain in Game.Core.</summary>
internal sealed class BattleScreen : IGameScreen
{
    private readonly PixelCanvas _canvas;
    private readonly AssetCatalog _assets;
    private readonly UnitCatalog _catalog;
    private readonly PixelFont _font;
    private readonly BattleHud _hud;
    private readonly Action _exit;
    private readonly int _seed;
    private readonly AStarPathfinder _pathfinder = new();
    private BattleInteraction _interaction = null!;
    private BattlefieldRenderer _field = null!;
    private HashSet<HexCoord> _reachable = [];
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private HexCoord? _hover;
    private bool _showGrid = true;
    private bool _healMode;
    private float _nightBlend;
    private float _nightTarget;
    private float _opponentDelay = 0.7f;
    private string _message = "ЛКМ: ПОКАЗАТЬ ПУТЬ. ПОВТОРНАЯ ЛКМ: ПЕРЕМЕСТИТЬ ОТРЯД.";
    private Battle Battle => _interaction.Battle;
    private static readonly Color Gold = new(218, 190, 113);
    private static readonly Color Green = new(149, 190, 107);
    private static readonly Color Red = new(210, 118, 89);
    private static readonly Rectangle RestartButton = new(444, 488, 186, 34);
    private static readonly Rectangle ExitButton = new(650, 488, 186, 34);

    public BattleScreen(PixelCanvas canvas, AssetCatalog assets, UnitCatalog catalog,
        int seed, bool night, Action exit, BattleCaptureStage capture = BattleCaptureStage.Opening)
    {
        _canvas = canvas;
        _assets = assets;
        _catalog = catalog;
        _font = new PixelFont(canvas);
        _hud = new BattleHud(canvas, assets, catalog);
        _exit = exit;
        _seed = seed;
        _nightBlend = _nightTarget = night ? 1f : 0f;
        Restart();
        if (capture != BattleCaptureStage.Opening)
        {
            _hover = BattleCaptureSetup.Advance(Battle, capture);
            _healMode = capture == BattleCaptureStage.Healing;
            _message = _healMode ? "H: ЛЕЧЕНИЕ. ЛКМ ПО РАНЕНОМУ СОЮЗНИКУ ВОССТАНОВИТ 10 ОЗ." : "БОЙ ЗАВЕРШЁН.";
            RefreshReachable();
        }
    }

    private void Restart()
    {
        var demo = BattleDemo.Create(_catalog, _seed);
        _interaction = new BattleInteraction(demo.Battle);
        _field = new BattlefieldRenderer(_canvas, _assets, demo.Field.Map, demo.Field.Props);
        _healMode = false;
        _hover = null;
        _opponentDelay = 0.7f;
        _message = "ЛКМ: ПОКАЗАТЬ ПУТЬ. ПОВТОРНАЯ ЛКМ: ПЕРЕМЕСТИТЬ ОТРЯД.";
        RefreshReachable();
    }

    public void Update(MouseState mouse, KeyboardState keyboard, float elapsedSeconds)
    {
        _nightBlend = BattleLighting.AdvanceFade(_nightBlend, _nightTarget, elapsedSeconds);
        _hover = mouse.Y < 600 ? _field.HitTest(mouse.Position) : null;
        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        if (Pressed(Keys.G) || clicked && BattleHud.GridButton.Contains(mouse.Position)) _showGrid = !_showGrid;
        if (Pressed(Keys.N)) _nightTarget = 1f - _nightTarget;

        if (Battle.IsFinished)
        {
            if (Pressed(Keys.R) || clicked && RestartButton.Contains(mouse.Position)) Restart();
            else if (clicked && ExitButton.Contains(mouse.Position)) _exit();
        }
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
            else if (Pressed(Keys.H) || clicked && BattleHud.HealButton.Contains(mouse.Position))
            {
                if (Battle.ActiveUnit is { Ability: UnitAbility.HealAlly } active && !Battle.HasAttacked(active.Id))
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
                if (actor is not null && action is not null)
                    _message = action.Type switch
                    {
                        BattleActionType.Attack => Name(actor) + ": АТАКА ПО " + Name(Find(action.TargetId)!),
                        BattleActionType.Move => Name(actor) + ": ПЕРЕМЕЩЕНИЕ.",
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
                _message = $"{Name(target)}: ВОССТАНОВЛЕНО {target.RemainingHitPoints - before} ОЗ.";
                _healMode = false;
            }
            else _message = "НУЖЕН ЖИВОЙ РАНЕНЫЙ СОЮЗНИК. СЕБЯ ЛЕЧИТЬ НЕЛЬЗЯ.";
        }
        else if (target is not null && target.Side != active.Side)
        {
            var before = target.RemainingHitPoints;
            if (_interaction.TryAttack(target.Id))
                _message = $"{Name(active)}: УРОН {before - target.RemainingHitPoints}. " +
                    (target.Side == active.Side ? "ВРАГ НА НАШЕЙ СТОРОНЕ ДО КОНЦА БОЯ!" : $"В ЦЕЛИ ОСТАЛОСЬ {target.Count}.");
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
        if (!_interaction.IsPlayerTurn || Battle.ActiveUnit is not { } active || Battle.HasMoved(active.Id)) return;
        var occupied = Battle.Units.Where(unit => unit.IsAlive && unit.Id != active.Id).Select(unit => unit.Position).ToHashSet();
        foreach (var tile in Battle.Map.Tiles)
        {
            if (tile.Coordinate == active.Position || occupied.Contains(tile.Coordinate)) continue;
            var path = _pathfinder.FindPath(Battle.Map, active.Position, tile.Coordinate, occupied);
            if (path.IsReachable && path.Cost <= active.Movement) _reachable.Add(tile.Coordinate);
        }
    }

    public void Draw()
    {
        _field.DrawTerrain(_showGrid, _nightBlend);
        DrawTacticalOverlay();
        _field.DrawProps(_nightBlend);
        foreach (var unit in Battle.Units.Where(unit => unit.IsAlive).OrderBy(unit => unit.Position.R).ThenBy(unit => unit.Position.Q))
            DrawUnit(unit);
        var hovered = _hover is { } cell ? Battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Position == cell) : null;
        _hud.Draw(Battle, DescribeHover(hovered), hovered, _healMode, _showGrid);
        if (Battle.IsFinished) DrawResult();
    }

    private void DrawTacticalOverlay()
    {
        if (_showGrid && !_healMode)
            foreach (var coordinate in _reachable)
            {
                _canvas.Polygon(_field.Layout.Corners(Battle.Map.GetTile(coordinate)!, 5), Green * 0.12f);
                _canvas.Outline(_field.Layout.Corners(Battle.Map.GetTile(coordinate)!, 5), Green * 0.4f, 1);
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
                    _canvas.Outline(_field.Layout.Corners(Battle.Map.GetTile(unit.Position)!, 5), healable ? Green : Red, 2);
            }
        }
        if (_hover is { } hover)
            _canvas.Outline(_field.Layout.Corners(Battle.Map.GetTile(hover)!, 2), Gold * 0.7f, 1);
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
            var bounds = new Rectangle(x - 28, y - 49, 56, 66);
            if (!_assets.TryDrawCropped(_canvas, key, bounds) &&
                !_assets.TryDraw(_canvas, AssetCatalog.HumanGuard, new Rectangle(x - 24, y - 42, 48, 64)))
                FantasyArt.DrawGuard(_canvas, x, y);
        }
        var label = unit.UnitTypeId switch
        {
            "human.militia" => "О " + unit.Count,
            "human.spearman" => "К " + unit.Count,
            "human.longbow" => "Л " + unit.Count,
            _ => unit.Count.ToString()
        };
        var width = label.Length * 6 + 8;
        _canvas.Rect(x + 12, y + 12, width, 13, new Color(35, 29, 23));
        _canvas.Frame(x + 12, y + 12, width, 13, unit.Side == BattleSide.Player ? Green : Red, 1);
        _font.Draw(label, x + 16, y + 15, Gold, 1);
    }

    private string DescribeHover(BattleUnit? unit)
    {
        if (Battle.IsFinished || !_interaction.IsPlayerTurn) return _message;
        if (unit is not null && Battle.ActiveUnit is { } active && unit.Id != active.Id)
        {
            if (_healMode) return unit.Side == active.Side ? "ЛКМ: ВОССТАНОВИТЬ 10 ОЗ В ПРЕДЕЛАХ НАЧАЛЬНОГО ЗДОРОВЬЯ."
                : "ВРАЖЕСКИЙ ОТРЯД НЕЛЬЗЯ ЛЕЧИТЬ.";
            if (unit.Side != active.Side)
                return Battle.HasAttacked(active.Id) ? "ДЕЙСТВИЕ УЖЕ ИСПОЛЬЗОВАНО." :
                    BattleRules.CanAttack(active, unit, Battle.Map) ? "ЛКМ: АТАКОВАТЬ. УРОН " + (long)active.Count * active.DamagePerCreature
                    : "ВРАГ НЕДОСТУПЕН: ДАЛЬНОСТЬ ИЛИ ЛИНИЯ ВИДИМОСТИ.";
        }
        if (_hover is { } coordinate && Battle.ActiveUnit is { } actor)
        {
            var height = Battle.Map.GetTile(coordinate)!.Height;
            var delta = height - actor.Height;
            if (delta != 0 && unit is null)
            {
                var enemies = Battle.GetEnemies(actor.Side).Select(target => target.Height);
                var preview = ElevationChangePreview.Between(actor.Height, height, enemies);
                return $"ВЫСОТА {height} ({delta:+0;-0}). " + (!actor.IsRanged ? "БОНУС ДАЛЬНОСТИ ТОЛЬКО У СТРЕЛКОВ." :
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
        _font.Draw("ПРОБНЫЙ БОЙ: АРМИЯ КАМПАНИИ НЕ ИЗМЕНЯЕТСЯ.", 402, 444, Gold, 1);
        ResultButton(RestartButton, "R: ПОВТОРИТЬ");
        ResultButton(ExitButton, "ESC: ВЫХОД");
    }

    private void ResultButton(Rectangle rect, string text)
    {
        _canvas.Frame(rect.X, rect.Y, rect.Width, rect.Height, Gold, 1);
        _font.Draw(text, rect.X + (rect.Width - text.Length * 6) / 2, rect.Y + 13, Gold, 1);
    }

    private BattleUnit? Find(string? id) => Battle.Units.FirstOrDefault(unit => unit.Id == id);
    private string Name(BattleUnit unit) => _catalog.Get(unit.UnitTypeId)?.Name ?? unit.UnitTypeId;
}
