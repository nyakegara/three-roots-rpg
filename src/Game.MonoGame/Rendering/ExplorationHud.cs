using Game.Core.Characters;
using Game.Core.Content;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Screen-fixed campaign bar styled as wood, bronze, and parchment.</summary>
internal sealed class ExplorationHud(PixelCanvas canvas, AssetCatalog assets, UnitCatalog? units = null)
{
    public static readonly Rectangle MapButton = new(1002, 619, 100, 69);
    public static readonly Rectangle JournalButton = new(1115, 619, 100, 69);
    private readonly PixelFont _font = new(canvas);
    private static readonly Color Wood = new(49, 35, 25);
    private static readonly Color Bronze = new(141, 107, 59);
    private static readonly Color Gold = new(226, 196, 126);
    private static readonly Color Parchment = new(218, 205, 162);

    public void Draw(Hero hero, IReadOnlyList<TroopStack> stacks, WorldPointOfInterest? hoveredPoint,
        string actionText)
    {
        canvas.Rect(0, 596, 1280, 124, Wood);
        canvas.Rect(0, 596, 1280, 4, new Color(37, 27, 20));
        canvas.Rect(0, 600, 1280, 2, Bronze);
        canvas.Rect(0, 604, 1280, 1, new Color(103, 78, 45));
        canvas.Rect(0, 716, 1280, 4, new Color(28, 21, 17));
        canvas.Frame(7, 603, 1266, 109, new Color(101, 76, 43), 2);

        canvas.Rect(17, 600, 94, 113, new Color(35, 26, 20));
        canvas.Frame(17, 600, 94, 113, Bronze, 3);
        canvas.Frame(22, 605, 84, 103, Gold * 0.68f, 1);
        var portrait = new Rectangle(27, 610, 74, 90);
        if (!assets.TryDraw(canvas, AssetCatalog.FloraPortrait, portrait)) FantasyArt.DrawPortrait(canvas, portrait, true);

        _font.Draw(hero.Name, 128, 614, Parchment, 2);
        _font.Draw($"УРОВЕНЬ {hero.Level}   ОПЫТ {hero.Experience}/{hero.ExperienceToNextLevel}", 128, 638, Gold, 1);
        var locationName = hoveredPoint?.Name.ToUpperInvariant() ?? "ПУТЕШЕСТВИЕ";
        if (locationName.Length > 30) locationName = locationName[..28] + "..";
        _font.Draw(locationName, 128, 662,
            hoveredPoint is null ? Parchment : Gold, 2);
        _font.Draw(actionText.Length <= 60 ? actionText : actionText[..58] + "..", 128, 688, new Color(172, 163, 132), 1);

        _font.Draw("ОТРЯД", 500, 608, new Color(175, 158, 114), 1);
        for (var slot = 0; slot < 5; slot++)
        {
            var x = 500 + slot * 61;
            canvas.Rect(x, 623, 54, 72, new Color(31, 27, 21));
            canvas.Frame(x, 623, 54, 72, slot < stacks.Count ? Bronze : new Color(91, 75, 50), 2);
            if (slot < stacks.Count)
            {
                var key = units?.Get(stacks[slot].UnitTypeId)?.ArtKey;
                if (key is null || !assets.TryDrawCropped(canvas, key, new Rectangle(x + 5, 627, 44, 42)))
                {
                    canvas.Rect(x + 16, 635, 22, 27, new Color(75, 91, 51));
                    canvas.Rect(x + 21, 629, 12, 10, new Color(198, 174, 118));
                }
                _font.Draw(stacks[slot].Count.ToString(), x + 7, 672, Parchment, 1);
            }
            else
            {
                canvas.Rect(x + 15, 686, 23, 1, new Color(73, 61, 43));
            }
        }

        canvas.Rect(824, 620, 1, 76, new Color(104, 79, 48));
        _font.Draw("ЗОЛОТО", 847, 628, new Color(174, 158, 116), 1);
        canvas.Rect(850, 650, 18, 18, Gold);
        canvas.Rect(854, 654, 10, 10, new Color(169, 118, 43));
        _font.Draw(hero.Gold.ToString(), 878, 651, Parchment, 2);

        DrawCommand(1002, "КАРТА", "M");
        DrawCommand(1115, "ЗАДАНИЯ", "J");
    }

    private void DrawCommand(int x, string label, string key)
    {
        canvas.Rect(x, 619, 100, 69, new Color(39, 30, 23));
        canvas.Frame(x, 619, 100, 69, Bronze, 2);
        canvas.Rect(x + 35, 627, 30, 27, new Color(104, 76, 40));
        canvas.Frame(x + 35, 627, 30, 27, Gold * 0.75f, 1);
        _font.Draw(key, x + 46, 635, Parchment, 1);
        _font.Draw(label, x + 9, 663, Parchment, 1);
    }
}
