using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Draws a low cross-road gate as one compact, readable pixel-art landmark.</summary>
internal static class AdventureGateArt
{
    private static readonly Color Shadow = new Color(30, 31, 20) * 0.72f;
    private static readonly Color Mortar = new(61, 56, 43);
    private static readonly Color Stone = new(117, 106, 78);
    private static readonly Color StoneLight = new(164, 148, 108);
    private static readonly Color StoneTop = new(194, 174, 126);
    private static readonly Color WoodDark = new(55, 33, 22);
    private static readonly Color Wood = new(119, 69, 35);
    private static readonly Color WoodLight = new(171, 111, 58);
    private static readonly Color Iron = new(48, 45, 36);

    public static void Draw(PixelCanvas canvas, int x, int y, bool isOpen)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        // A single ground footprint keeps both state sprites anchored identically.
        canvas.Rect(x - 39, y + 22, 22, 4, Shadow * 0.62f);
        canvas.Rect(x - 15, y + 22, 30, 4, Shadow * 0.72f);
        canvas.Rect(x + 17, y + 22, 22, 4, Shadow * 0.62f);
        DrawRoadThreshold(canvas, x, y + 14);
        DrawWallArm(canvas, x - 62, y + 1, 31);
        DrawWallArm(canvas, x + 62, y + 1, 31);
        DrawPier(canvas, x - 38, y - 1);
        DrawPier(canvas, x + 38, y - 1);

        if (isOpen)
        {
            // The two leaves fold back parallel to the north-south road.
            DrawOpenLeaf(canvas, x - 49, y - 13, true);
            DrawOpenLeaf(canvas, x + 37, y - 13, false);
        }
        else
        {
            // One broad barred leaf lies across the road between the two stone piers.
            DrawClosedLeaf(canvas, x, y + 1);
        }
    }

    private static void DrawRoadThreshold(PixelCanvas canvas, int x, int y)
    {
        canvas.Rect(x - 41, y - 7, 82, 14, Mortar);
        canvas.Rect(x - 38, y - 5, 76, 10, new Color(126, 103, 65));
        for (var offset = -30; offset <= 30; offset += 12)
        {
            canvas.Rect(x + offset, y - 4, 7, 2, StoneTop * 0.64f);
            canvas.Rect(x + offset + 3, y + 1, 7, 2, Mortar * 0.72f);
        }
    }

    private static void DrawWallArm(PixelCanvas canvas, int centerX, int y, int width)
    {
        canvas.Rect(centerX - width / 2 - 2, y + 5, width + 4, 12, Shadow);
        canvas.Rect(centerX - width / 2, y + 1, width, 13, Mortar);
        canvas.Rect(centerX - width / 2 + 2, y, width - 4, 10, Stone);
        canvas.Rect(centerX - width / 2 + 4, y + 2, width - 9, 2, StoneLight);
        for (var offset = 11; offset < width - 5; offset += 14)
            canvas.Rect(centerX - width / 2 + offset, y + 5, 2, 4, Mortar);
        canvas.Rect(centerX - width / 2 + 3, y - 4, width - 6, 5, StoneTop);
        canvas.Rect(centerX - width / 2 + 5, y - 3, width - 11, 2, StoneLight);
    }

    private static void DrawPier(PixelCanvas canvas, int x, int y)
    {
        canvas.Rect(x - 14, y - 12, 30, 32, Shadow);
        canvas.Rect(x - 12, y - 15, 26, 31, Mortar);
        canvas.Rect(x - 10, y - 14, 21, 27, Stone);
        canvas.Rect(x - 8, y - 12, 4, 20, StoneLight);
        canvas.Rect(x + 6, y - 12, 2, 20, new Color(82, 75, 55));
        canvas.Rect(x - 8, y - 6, 17, 2, Mortar);
        canvas.Rect(x - 8, y + 1, 17, 2, Mortar);
        canvas.Rect(x - 9, y - 17, 21, 4, StoneTop);
        canvas.Rect(x - 7, y - 16, 4, 2, StoneLight);
        canvas.Rect(x - 3, y - 15, 3, 2, Stone);
        canvas.Rect(x + 2, y - 16, 4, 2, StoneLight);
    }

    private static void DrawClosedLeaf(PixelCanvas canvas, int x, int y)
    {
        canvas.Rect(x - 31, y - 7, 62, 14, WoodDark);
        canvas.Rect(x - 28, y - 5, 56, 10, Wood);
        canvas.Rect(x - 26, y - 4, 52, 2, WoodLight);
        for (var offset = -24; offset <= 24; offset += 12)
        {
            canvas.Rect(x + offset - 1, y - 4, 3, 9, Iron);
            canvas.Rect(x + offset, y - 3, 1, 6, WoodLight);
        }
        canvas.Rect(x - 29, y - 2, 58, 2, Iron);
        canvas.Rect(x - 29, y + 2, 58, 2, Iron);
        canvas.Rect(x - 31, y - 9, 62, 2, StoneTop);
        canvas.Rect(x - 31, y + 6, 62, 2, Mortar);
    }

    private static void DrawOpenLeaf(PixelCanvas canvas, int x, int y, bool left)
    {
        var shadowX = left ? x + 2 : x + 6;
        canvas.Rect(shadowX, y + 3, 10, 35, Shadow);
        canvas.Rect(x + 1, y, 10, 34, WoodDark);
        canvas.Rect(x + 3, y + 2, 6, 30, Wood);
        canvas.Rect(x + (left ? 4 : 7), y + 4, 1, 25, WoodLight);
        canvas.Rect(x + 6, y + 3, 1, 28, WoodDark);
        canvas.Rect(x + 3, y + 9, 6, 2, Iron);
        canvas.Rect(x + 3, y + 23, 6, 2, Iron);
    }
}
