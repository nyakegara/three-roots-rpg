using Game.Core.Combat.Generation;
using Microsoft.Xna.Framework;

namespace Game.MonoGame;

/// <summary>Compact pixel props for generated combat terrain.</summary>
internal static class BattlefieldPropArt
{
    public static void Draw(PixelCanvas canvas, BattlefieldPropKind kind, int x, int y)
    {
        switch (kind)
        {
            case BattlefieldPropKind.Rock:
                DrawRock(canvas, x, y);
                break;
            case BattlefieldPropKind.FallenLog:
                DrawLog(canvas, x, y);
                break;
            case BattlefieldPropKind.Shrub:
                DrawShrub(canvas, x, y);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown battlefield prop.");
        }
    }

    private static void DrawRock(PixelCanvas canvas, int x, int y)
    {
        canvas.Rect(x - 18, y + 11, 36, 5, new Color(47, 61, 31));
        canvas.Polygon([new(x - 18, y + 10), new(x - 14, y - 7), new(x - 4, y - 16),
            new(x + 9, y - 14), new(x + 17, y - 3), new(x + 19, y + 10), new(x + 2, y + 15)],
            new Color(75, 78, 63));
        canvas.Polygon([new(x - 13, y + 7), new(x - 11, y - 6), new(x - 3, y - 13),
            new(x + 6, y - 11), new(x + 10, y - 2), new(x + 1, y + 2)], new Color(143, 142, 109));
        canvas.Polygon([new(x + 1, y + 2), new(x + 10, y - 2), new(x + 15, y + 8),
            new(x + 2, y + 12)], new Color(101, 107, 83));
        canvas.Rect(x - 10, y - 5, 3, 8, new Color(176, 166, 125));
    }

    private static void DrawLog(PixelCanvas canvas, int x, int y)
    {
        canvas.Rect(x - 25, y + 11, 50, 4, new Color(48, 59, 30));
        canvas.Polygon([new(x - 23, y + 3), new(x - 19, y - 4), new(x + 21, y - 4),
            new(x + 25, y + 1), new(x + 19, y + 9), new(x - 18, y + 9)], new Color(85, 57, 31));
        canvas.Rect(x - 17, y - 1, 33, 3, new Color(133, 93, 47));
        canvas.Rect(x - 14, y + 4, 31, 2, new Color(58, 48, 29));
        canvas.Rect(x - 24, y + 1, 6, 7, new Color(128, 92, 46));
        canvas.Rect(x + 19, y - 1, 6, 7, new Color(121, 83, 41));
        canvas.Rect(x - 22, y + 3, 2, 3, new Color(57, 48, 31));
        canvas.Rect(x + 21, y + 1, 2, 3, new Color(57, 48, 31));
        canvas.Polygon([new(x - 4, y - 3), new(x + 2, y - 7), new(x + 8, y - 3),
            new(x + 5, y + 1), new(x - 1, y + 1)], new Color(75, 77, 49));
    }

    private static void DrawShrub(PixelCanvas canvas, int x, int y)
    {
        canvas.Rect(x - 19, y + 10, 38, 5, new Color(48, 60, 31));
        canvas.Polygon([new(x - 20, y + 8), new(x - 15, y - 2), new(x - 7, y - 7),
            new(x - 1, y - 12), new(x + 8, y - 8), new(x + 16, y - 2), new(x + 20, y + 8),
            new(x + 8, y + 12)], new Color(39, 73, 34));
        canvas.Polygon([new(x - 14, y + 4), new(x - 10, y - 3), new(x - 1, y - 7),
            new(x + 5, y - 4), new(x + 14, y + 4), new(x + 7, y + 8)], new Color(75, 111, 43));
        canvas.Rect(x - 8, y - 5, 5, 3, new Color(120, 143, 60));
        canvas.Rect(x + 4, y - 2, 4, 3, new Color(124, 145, 58));
        canvas.Rect(x - 2, y + 3, 4, 3, new Color(53, 91, 37));
    }
}
