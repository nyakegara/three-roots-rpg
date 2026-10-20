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
            // The hinged panels fold toward the top and bottom of the road, leaving a clear gap.
            DrawOpenLeaf(canvas, x - 18, y - 18, true);
            DrawOpenLeaf(canvas, x + 9, y + 2, false);
            canvas.Rect(x - 4, y - 13, 8, 26, new Color(231, 203, 139) * 0.22f);
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
        canvas.Rect(x - 13, y - 34, 5, 48, StoneLight);
        canvas.Rect(x - 9, y - 28, 19, 2, Mortar);
        canvas.Rect(x - 9, y - 16, 19, 2, Mortar);
        canvas.Rect(x - 9, y - 4, 19, 2, Mortar);
        canvas.Rect(x - 8, y - 25, 3, 8, StoneTop);
        canvas.Rect(x - 8, y - 1, 3, 8, StoneTop);
        canvas.Rect(x - 7, y - 30, 14, 12, Mortar);
        canvas.Rect(x - 4, y - 27, 8, 9, new Color(36, 32, 26));
        canvas.Rect(x - 22, y - 40, 44, 9, Mortar);
        canvas.Rect(x - 19, y - 39, 38, 5, StoneTop);
        canvas.Rect(x - 18, y - 34, 7, 6, StoneLight);
        canvas.Rect(x - 3, y - 34, 7, 6, StoneLight);
        canvas.Rect(x + 12, y - 34, 7, 6, StoneLight);
        canvas.Rect(x - 5, y + 7, 10, 12, WoodDark);
        canvas.Rect(x - 3, y + 9, 6, 10, new Color(39, 34, 26));
    }

    private static void DrawOpenLeaf(PixelCanvas canvas, int x, int y, bool hingeTop)
    {
        canvas.Rect(x, y, 10, 22, WoodDark);
        canvas.Rect(x + 2, y + 2, 6, 18, Wood);
        canvas.Rect(x + 3, y + (hingeTop ? 4 : 14), 4, 3, WoodLight);
        canvas.Rect(x + 1, y + (hingeTop ? 2 : 18), 8, 2, Iron);
    }
}
