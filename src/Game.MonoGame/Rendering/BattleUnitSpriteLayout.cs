using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>One resting sprite footprint for both drawing and mouse picking.</summary>
internal static class BattleUnitSpriteLayout
{
    public static Rectangle ActionBounds(string unitTypeId, Rectangle restingBounds)
    {
        // The hunter crouches: filling the standing box enlarged its face and torso.
        // This pose-specific calibration keeps its feet planted; other art keeps its own scale.
        var scale = unitTypeId == "human.order-hunter" ? 0.88f : 1f;
        var width = (int)MathF.Round((restingBounds.Width + 22) * scale);
        var height = (int)MathF.Round(restingBounds.Height * scale);
        return new Rectangle(restingBounds.Center.X - width / 2, restingBounds.Bottom - height, width, height);
    }

    public static Rectangle Bounds(string unitTypeId, Point center)
    {
        var (width, height) = unitTypeId switch
        {
            "human.inquisitor" => (88, 100),
            "human.banner-knight" => (83, 94),
            "flora.root-warden" => (75, 88),
            "undead.utburd" => (64, 88),
            "undead.silat" => (56, 66),
            "undead.nachzehrer" => (84, 70),
            "undead.vetala" => (68, 94),
            "undead.asura-preta" => (112, 108),
            _ => (56, 66)
        };
        return new Rectangle(center.X - width / 2, center.Y + 17 - height, width, height);
    }

    public static string CountLabel(string unitTypeId, int count) => unitTypeId switch
    {
        "human.militia" => "О " + count,
        "human.banner-knight" => "Р " + count,
        "human.longbow" => "Л " + count,
        _ => count.ToString()
    };

    public static Rectangle CountBounds(string label, Point center) =>
        new(center.X + 12, center.Y + 12, label.Length * 6 + 8, 13);
}
