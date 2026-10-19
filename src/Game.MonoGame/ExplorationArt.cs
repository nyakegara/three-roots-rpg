using Microsoft.Xna.Framework;

namespace Game.MonoGame;

/// <summary>Small pixel-art map props with optional replaceable PNG overrides.</summary>
internal static class ExplorationArt
{
    private static readonly Color Bark = new(67, 49, 28);
    private static readonly Color DeepLeaf = new(35, 62, 30);
    private static readonly Color Leaf = new(73, 108, 42);
    private static readonly Color LightLeaf = new(133, 151, 68);

    public static void DrawForestTree(PixelCanvas canvas, int x, int groundY)
    {
        FantasyArt.Oval(canvas, x, groundY + 7, 22, 6, new Color(40, 53, 28));
        canvas.Rect(x - 4, groundY - 21, 8, 30, Bark);
        canvas.Line(new Vector2(x, groundY - 5), new Vector2(x - 17, groundY - 20), Bark, 4);
        canvas.Line(new Vector2(x + 1, groundY - 11), new Vector2(x + 15, groundY - 25), Bark, 4);
        FantasyArt.Oval(canvas, x - 13, groundY - 26, 19, 16, DeepLeaf);
        FantasyArt.Oval(canvas, x + 10, groundY - 32, 20, 18, DeepLeaf);
        FantasyArt.Oval(canvas, x - 1, groundY - 42, 20, 18, DeepLeaf);
        FantasyArt.Oval(canvas, x - 14, groundY - 29, 15, 12, Leaf);
        FantasyArt.Oval(canvas, x + 8, groundY - 35, 16, 13, Leaf);
        FantasyArt.Oval(canvas, x - 2, groundY - 45, 16, 13, LightLeaf);
        canvas.Rect(x - 10, groundY - 42, 5, 2, new Color(153, 167, 79));
        canvas.Rect(x + 5, groundY - 49, 5, 2, new Color(153, 167, 79));
    }

    public static void DrawAncientOak(PixelCanvas canvas, int x, int groundY)
    {
        FantasyArt.Oval(canvas, x, groundY + 12, 38, 9, new Color(39, 51, 26));
        canvas.Polygon([new(x - 7, groundY - 29), new(x + 8, groundY - 28),
            new(x + 14, groundY + 13), new(x + 27, groundY + 20), new(x + 8, groundY + 16),
            new(x, groundY + 7), new(x - 9, groundY + 18), new(x - 25, groundY + 21),
            new(x - 12, groundY + 10)], Bark);
        canvas.Line(new Vector2(x, groundY - 11), new Vector2(x - 29, groundY - 34), Bark, 8);
        canvas.Line(new Vector2(x + 5, groundY - 15), new Vector2(x + 28, groundY - 40), Bark, 7);
        FantasyArt.Oval(canvas, x - 27, groundY - 46, 31, 23, DeepLeaf);
        FantasyArt.Oval(canvas, x + 21, groundY - 51, 32, 25, DeepLeaf);
        FantasyArt.Oval(canvas, x - 3, groundY - 65, 34, 27, DeepLeaf);
        FantasyArt.Oval(canvas, x - 27, groundY - 50, 25, 18, Leaf);
        FantasyArt.Oval(canvas, x + 19, groundY - 55, 25, 20, Leaf);
        FantasyArt.Oval(canvas, x - 4, groundY - 69, 27, 21, LightLeaf);
        canvas.Rect(x - 27, groundY - 60, 7, 3, new Color(150, 166, 77));
        canvas.Rect(x + 11, groundY - 68, 8, 3, new Color(150, 166, 77));
        canvas.Rect(x + 36, groundY - 49, 6, 3, new Color(150, 166, 77));
    }

    public static void DrawTreasureChest(PixelCanvas canvas, int x, int groundY)
    {
        FantasyArt.Oval(canvas, x, groundY + 4, 18, 5, new Color(40, 50, 28));
        canvas.Polygon([new(x - 16, groundY - 9), new(x - 10, groundY - 18),
            new(x + 10, groundY - 18), new(x + 16, groundY - 9),
            new(x + 14, groundY + 7), new(x - 14, groundY + 7)], new Color(82, 48, 28));
        canvas.Rect(x - 14, groundY - 8, 28, 13, new Color(139, 88, 42));
        canvas.Rect(x - 11, groundY - 16, 22, 8, new Color(169, 111, 49));
        canvas.Rect(x - 3, groundY - 15, 6, 20, new Color(197, 159, 73));
        canvas.Rect(x - 2, groundY - 4, 4, 5, new Color(79, 57, 30));
        canvas.Rect(x - 14, groundY + 3, 28, 3, new Color(92, 58, 31));
        canvas.Rect(x - 8, groundY - 13, 3, 3, new Color(220, 190, 101));
    }
}
