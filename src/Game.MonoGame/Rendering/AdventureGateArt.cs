using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Draws one north-road gate as a pixel-art world object with locked and open states.</summary>
internal static class AdventureGateArt
{
    private static readonly Color Shadow = new Color(35, 35, 23) * 0.72f;
    private static readonly Color Mortar = new(57, 54, 43);
    private static readonly Color Stone = new(112, 103, 77);
    private static readonly Color StoneLight = new(160, 145, 105);
    private static readonly Color StoneTop = new(189, 169, 119);
    private static readonly Color WoodDark = new(63, 39, 25);
    private static readonly Color Wood = new(122, 72, 35);
    private static readonly Color WoodLight = new(165, 105, 54);
    private static readonly Color Iron = new(50, 48, 39);

    public static void Draw(PixelCanvas canvas, int x, int y, bool isOpen)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.Rect(x - 76, y + 24, 152, 17, Shadow);

        // The road runs north–south. A cross-wall closes it; two square towers anchor its ends.
        DrawWall(canvas, x - 62, y - 4, 42, 17);
        DrawWall(canvas, x + 20, y - 4, 42, 17);
        DrawTower(canvas, x - 63, y - 2);
        DrawTower(canvas, x + 63, y - 2);

        if (isOpen)
        {
            // Both leaves fold along the road so the open state keeps a broad, readable passage.
            DrawOpenLeaf(canvas, x - 25, y - 15, true);
            DrawOpenLeaf(canvas, x + 9, y - 15, false);
        }
        else
        {
            // A stout timber portcullis spans the road between the wall arms.
            canvas.Rect(x - 22, y - 12, 44, 25, WoodDark);
            canvas.Rect(x - 18, y - 9, 36, 19, Wood);
            canvas.Rect(x - 18, y - 9, 36, 3, WoodLight);
            for (var offset = -14; offset <= 14; offset += 7)
                canvas.Rect(x + offset - 2, y - 7, 4, 16, Iron);
            canvas.Rect(x - 18, y - 4, 36, 3, Iron);
            canvas.Rect(x - 18, y + 4, 36, 3, Iron);
            canvas.Rect(x - 22, y - 13, 44, 3, StoneTop);
            canvas.Rect(x - 22, y + 11, 44, 3, Mortar);
        }
    }

    private static void DrawWall(PixelCanvas canvas, int x, int y, int width, int height)
    {
        canvas.Rect(x - width / 2 - 2, y + 4, width + 4, height, Mortar);
        canvas.Rect(x - width / 2, y, width, height, Stone);
        canvas.Rect(x - width / 2 + 3, y + 2, width - 8, 3, StoneLight);
        canvas.Rect(x - width / 2 + 4, y + 8, width - 9, 2, Mortar);
        for (var offset = 4; offset < width - 4; offset += 10)
            canvas.Rect(x - width / 2 + offset, y + 5, 2, 6, StoneLight);
        for (var offset = -width / 2; offset < width / 2; offset += 12)
        {
            canvas.Rect(x + offset + 2, y - 4, 8, 5, StoneTop);
            canvas.Rect(x + offset + 4, y - 2, 4, 2, StoneLight);
        }
    }

    private static void DrawTower(PixelCanvas canvas, int x, int y)
    {
        canvas.Rect(x - 20, y - 34, 40, 61, Shadow);
        canvas.Rect(x - 19, y - 37, 38, 59, Mortar);
        canvas.Rect(x - 16, y - 36, 32, 54, Stone);
        canvas.Rect(x + 9, y - 33, 6, 47, new Color(78, 72, 54));
        canvas.Rect(x - 13, y - 34, 4, 47, StoneLight);
        canvas.Rect(x - 12, y - 23, 24, 2, Mortar);
        canvas.Rect(x - 12, y - 9, 24, 2, Mortar);
        canvas.Rect(x - 12, y + 5, 24, 2, Mortar);
        canvas.Rect(x - 7, y - 19, 2, 8, StoneTop);
        canvas.Rect(x + 5, y - 5, 2, 8, StoneLight);
        canvas.Rect(x - 7, y - 30, 14, 12, Mortar);
        canvas.Rect(x - 4, y - 27, 8, 9, new Color(36, 32, 26));
        DrawMerlon(canvas, x - 16, y - 42, 12, 12);
        DrawMerlon(canvas, x - 6, y - 46, 12, 16);
        DrawMerlon(canvas, x + 4, y - 42, 12, 12);
        canvas.Rect(x - 5, y + 7, 10, 12, WoodDark);
        canvas.Rect(x - 3, y + 9, 6, 10, new Color(39, 34, 26));
    }

    private static void DrawMerlon(PixelCanvas canvas, int x, int y, int width, int height)
    {
        canvas.Rect(x, y, width, height, Mortar);
        canvas.Rect(x + 2, y + 1, width - 4, height - 1, StoneTop);
        canvas.Rect(x + 3, y + 2, 3, height - 4, StoneLight);
        canvas.Rect(x + 4, y + height - 3, width - 7, 2, Stone);
    }

    private static void DrawOpenLeaf(PixelCanvas canvas, int x, int y, bool hingedLeft)
    {
        canvas.Rect(x, y, 16, 30, WoodDark);
        canvas.Rect(x + 2, y + 2, 12, 26, Wood);
        canvas.Rect(x + (hingedLeft ? 3 : 10), y + 4, 2, 20, WoodLight);
        canvas.Rect(x + 6, y + 3, 1, 24, WoodDark);
        canvas.Rect(x + 2, y + 8, 12, 3, Iron);
        canvas.Rect(x + 2, y + 21, 12, 3, Iron);
        canvas.Rect(x + 3, y + 9, 3, 1, StoneTop);
        canvas.Rect(x + 4, y + 23, 3, 1, StoneTop);
    }
}
