using Game.Core.Combat;
using Game.Core.Content;
using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Compact wood-and-bronze battle commands, unit details, and initiative strip.</summary>
internal sealed class BattleHud(PixelCanvas canvas, AssetCatalog assets, UnitCatalog units)
{
    public static readonly Rectangle EndTurnButton = new(1040, 635, 100, 34);
    public static readonly Rectangle HealButton = new(930, 635, 96, 34);
    public static readonly Rectangle GridButton = new(1154, 635, 108, 34);

    private readonly PixelFont _font = new(canvas);
    private static readonly Color Shadow = new(27, 24, 16);
    private static readonly Color Wood = new(46, 37, 26);
    private static readonly Color Bronze = new(133, 107, 63);
    private static readonly Color Gold = new(201, 172, 102);
    private static readonly Color Text = new(221, 211, 174);
    private static readonly Color Muted = new(164, 158, 129);
    private static readonly Color Ally = new(145, 176, 101);
    private static readonly Color Enemy = new(190, 114, 83);

    public void Draw(Battle battle, string message, BattleUnit? hoveredUnit, bool healMode, bool showGrid)
    {
        DrawRail();
        var active = battle.ActiveUnit;
        var inspected = hoveredUnit ?? active;
        var playerTurn = !battle.IsFinished && active?.Side == BattleSide.Player;
        var canHeal = playerTurn && active is { Ability: UnitAbility.HealAlly } &&
                      !battle.HasAttacked(active.Id);
        var canChangeMode = playerTurn && active is { Ability: UnitAbility.RadiantModes } &&
                            !battle.HasAttacked(active.Id);

        if (inspected is not null)
        {
            var name = units.Get(inspected.UnitTypeId)?.Name ?? inspected.UnitTypeId;
            var prefix = hoveredUnit is null ? "ХОД: " : "ЦЕЛЬ: ";
            _font.Draw(Shorten(prefix + name, 42), 110, 624, Text, 2);
            var range = inspected.Ability == UnitAbility.RadiantModes && inspected.AttackMode == AttackMode.HolyFire
                ? 4 : inspected.Range;
            var mode = inspected.Ability == UnitAbility.RadiantModes
                ? inspected.AttackMode == AttackMode.HolyFire ? "   РЕЖИМ: ОГОНЬ" : "   РЕЖИМ: МОЛОТ" : "";
            var details = $"БОЙЦОВ {inspected.Count}   ОЗ {inspected.RemainingHitPoints}/" +
                          $"{inspected.MaximumHitPointsAtBattleStart}   УРОН {inspected.DamagePerCreature} " +
                          $"Х {inspected.Count}   ХОД {inspected.Movement}   ДАЛЬН. {range}{mode}";
            _font.Draw(Shorten(details, 128), 110, 645, Muted, 1);
        }
        else
            _font.Draw(battle.IsFinished ? "БОЙ ЗАВЕРШЁН" : "НАЧАЛО РАУНДА", 110, 624, Text, 2);

        var sideText = battle.IsFinished ? "ИТОГ" : active?.Side == BattleSide.Player ? "ВАШ ХОД" : "ХОД ВРАГА";
        _font.Draw($"РАУНД {battle.Round}  {sideText}", 660, 626, Gold, 1);
        _font.Draw(Shorten(message, 65), 110, 658, healMode && canHeal ? Gold : Text, 2);

        DrawInitiative(battle, active);
        var activation = playerTurn && active is not null
            ? $"ДВИЖЕНИЕ: {(battle.HasMoved(active.Id) ? "ИСП." : "ЕСТЬ")}   " +
              $"ДЕЙСТВИЕ: {(battle.HasAttacked(active.Id) ? "ИСП." : "ЕСТЬ")}" : "";
        _font.Draw(activation, 450, 684, Muted, 1);
        _font.Draw("ЛКМ: ПУТЬ / ЦЕЛЬ    ESC: ВЫХОД    N: ДЕНЬ / НОЧЬ", 450, 701, Muted, 1);

        DrawButton(HealButton, canChangeMode ? "M: РЕЖИМ" : "H: ЛЕЧИТЬ",
            canHeal || canChangeMode, canHeal && healMode || canChangeMode && active!.AttackMode == AttackMode.HolyFire);
        DrawButton(EndTurnButton, "ENTER: ХОД", playerTurn, false);
        DrawButton(GridButton, "G: СЕТКА", true, showGrid);
        if (canHeal) _font.Draw("ЛЕЧЕНИЕ: ВЫБЕРИТЕ СОЮЗНИКА", 930, 684, Muted, 1);
        _font.Draw("ENTER: ЗАВЕРШИТЬ ХОД", 930, 701, Muted, 1);
    }

    private void DrawRail()
    {
        canvas.Rect(0, 610, 1280, 110, Wood);
        for (var y = 640; y < 712; y += 34)
            canvas.Rect(0, y, 1280, 1, new Color(35, 30, 22));
        canvas.Rect(0, 610, 1280, 2, Bronze);
        canvas.Rect(0, 613, 1280, 1, Gold * 0.6f);
        canvas.Rect(0, 714, 1280, 3, Bronze);
        canvas.Rect(0, 718, 1280, 2, Shadow);
        canvas.Rect(14, 610, 78, 94, Shadow);
        canvas.Frame(14, 610, 78, 94, Bronze, 2);
        var portrait = new Rectangle(18, 612, 70, 86);
        if (!assets.TryDraw(canvas, AssetCatalog.FloraPortrait, portrait))
            FantasyArt.DrawPortrait(canvas, portrait, true);
        _font.Draw("ФЛОРА", 34, 706, Gold, 1);
    }

    private void DrawInitiative(Battle battle, BattleUnit? active)
    {
        _font.Draw("ИНИЦИАТИВА", 110, 675, Muted, 1);
        var living = battle.Units.Where(unit => unit.IsAlive)
            .OrderByDescending(unit => unit.Speed).ThenBy(unit => unit.Id, StringComparer.Ordinal)
            .Take(8).ToArray();
        for (var index = 0; index < living.Length; index++)
        {
            var unit = living[index];
            var x = 120 + index * 39;
            var isActive = active?.Id == unit.Id;
            var color = unit.Side == BattleSide.Player ? Ally : Enemy;
            canvas.Rect(x, 685, 34, 28, Shadow);
            canvas.Frame(x, 685, 34, 28, isActive ? Gold : Bronze, isActive ? 2 : 1);
            canvas.Rect(x + 3, 688, 2, 21, color);
            var icon = new Rectangle(x + 7, 687, 22, 19);
            var definition = units.Get(unit.UnitTypeId);
            var key = unit.IsHero ? AssetCatalog.FloraHero : definition?.ArtKey ?? AssetCatalog.HumanGuard;
            if (!assets.TryDrawCropped(canvas, key, icon) &&
                !assets.TryDrawCropped(canvas, AssetCatalog.HumanGuard, icon))
            {
                canvas.Rect(x + 14, 690, 7, 6, Gold);
                canvas.Rect(x + 11, 696, 13, 8, color);
            }
            var count = unit.Count.ToString();
            canvas.Rect(x + 7, 705, 25, 7, Shadow);
            _font.Draw(Shorten(count, 4), x + 7, 705, isActive ? Gold : Text, 1);
        }
    }

    private void DrawButton(Rectangle bounds, string label, bool enabled, bool selected)
    {
        canvas.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height,
            enabled ? new Color(52, 45, 31) : new Color(38, 33, 26));
        canvas.Frame(bounds.X, bounds.Y, bounds.Width, bounds.Height,
            selected ? Gold : enabled ? Bronze : new Color(79, 65, 44), selected ? 2 : 1);
        if (selected)
            canvas.Rect(bounds.X + 4, bounds.Bottom - 5, bounds.Width - 8, 1, Gold);
        var x = bounds.X + (bounds.Width - label.Length * 6 + 1) / 2;
        _font.Draw(label, x, bounds.Y + 13, enabled ? selected ? Gold : Text : new Color(112, 104, 83), 1);
    }

    private static string Shorten(string text, int maximumLength) =>
        text.Length <= maximumLength ? text : text[..(maximumLength - 2)] + "..";
}
