using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>One resting sprite footprint for both drawing and mouse picking.</summary>
internal static class BattleUnitSpriteLayout
{
    public static Rectangle Bounds(string unitTypeId, Point center)
    {
        var (width, height) = unitTypeId switch
        {
            "human.inquisitor" => (88, 100),
            "human.banner-knight" => (83, 94),
            "flora.root-warden" => (75, 88),
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
