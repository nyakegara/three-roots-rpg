using Microsoft.Xna.Framework;

namespace Game.MonoGame;

/// <summary>Original pixel-art fallbacks. PNG overrides remain separate from game rules.</summary>
internal static class FantasyArt
{
    private static readonly Color Bark = new(59, 48, 28);
    private static readonly Color DeepLeaf = new(36, 62, 31);
    private static readonly Color Leaf = new(78, 111, 43);
    private static readonly Color LightLeaf = new(129, 151, 66);

    public static void DrawLandscape(PixelCanvas canvas, Color? skyColor = null)
    {
        canvas.Rect(0, 0, 1280, 610, new Color(90, 112, 48));
        for (var y = 0; y < 610; y += 4)
        {
            var light = Math.Clamp((y - 35) / 22, 0, 9);
            canvas.Rect(0, y, 1280, 4, new Color(75 + light, 99 + light, 39 + light / 2));
        }

        // Quiet irregular patches connect the field into one landscape instead of separate tiles.
        for (var index = 0; index < 65; index++)
        {
            var seed = Noise(index * 17, 67);
            var x = seed % 1280;
            var y = 95 + seed / 17 % 490;
            var earthy = index % 5 == 0;
            Oval(canvas, x, y, 18 + seed % 60, 6 + seed % 15,
                earthy ? new Color(99, 103, 53) : new Color(82, 106, 43));
            Oval(canvas, x - 9, y - 3, 12 + seed % 35, 4 + seed % 8,
                earthy ? new Color(106, 108, 59) : new Color(87, 112, 45));
        }

        for (var y = 88; y < 606; y += 7)
        for (var x = 0; x < 1280; x += 7)
        {
            var seed = Noise(x, y);
            if (seed % 3 == 0)
                canvas.Rect(x + seed % 5, y, 2 + seed % 4, 1,
                    seed % 2 == 0 ? new Color(116, 133, 58) : new Color(67, 92, 37));
            if (seed % 19 == 0)
            {
                canvas.Rect(x + 2, y - 3, 1, 4, new Color(126, 145, 61));
                canvas.Rect(x, y - 1, 1, 2, new Color(106, 130, 49));
            }
        }

        if (skyColor is { } sky)
        {
            var horizon = Color.Lerp(sky, new Color(95, 112, 92), 0.42f);
            for (var y = 0; y < 88; y += 4)
            {
                var color = Color.Lerp(sky, horizon, y / 88f);
                canvas.Rect(0, y, 1280, 4, color);
            }
        }

        // A wooded edge frames the clearing; it is scenery, not additional occupied cells.
        DrawTree(canvas, -14, 37, 4);
        DrawTree(canvas, 132, -6, 21);
        DrawTree(canvas, 275, 32, 38);
        DrawTree(canvas, 457, -18, 55);
        DrawTree(canvas, 662, 6, 72);
        DrawTree(canvas, 819, 34, 89);
        DrawTree(canvas, 1050, -12, 106);
        DrawTree(canvas, 1255, 28, 123);
        DrawTree(canvas, -17, 238, 22);
        DrawTree(canvas, 1290, 214, 31);
        DrawTree(canvas, -29, 439, 41);
        DrawTree(canvas, 1308, 456, 45);

        for (var index = 0; index < 24; index++)
        {
            var x = Noise(index, 13) % 1280;
            var y = 94 + Noise(index, 29) % 485;
            if (x is > 65 and < 1210 && y is > 135 and < 570)
                continue;
            Oval(canvas, x, y, 8 + index % 5, 4, new Color(64, 75, 41));
            canvas.Rect(x - 2, y - 3, 7, 2, new Color(138, 137, 88));
        }
    }

    private static void DrawTree(PixelCanvas canvas, int x, int y, int seed)
    {
        if (seed % 4 == 1)
        {
            // Narrow layered firs interrupt the broad deciduous silhouettes.
            Oval(canvas, x + 8, y + 66, 37, 9, new Color(44, 65, 30));
            canvas.Rect(x - 4, y + 13, 10, 56, Bark);
            for (var layer = 2; layer >= 0; layer--)
            {
                var top = y - 59 + layer * 20;
                var width = 24 + layer * 13;
                canvas.Polygon([new(x - 4, top - 21), new(x + width, top + 39),
                    new(x + 9, top + 32), new(x + width - 7, top + 45),
                    new(x - width, top + 42), new(x - 13, top + 28),
                    new(x - width + 7, top + 30)], DeepLeaf);
                canvas.Polygon([new(x - 5, top - 15), new(x + 11, top + 20),
                    new(x - 7, top + 16), new(x - width + 12, top + 33)], new Color(71, 103, 39));
            }
            return;
        }

        var slender = seed % 4 == 2;
        Oval(canvas, x + 12, y + 64, 58, 13, new Color(43, 64, 30));
        canvas.Polygon([new(x - 9, y + 12), new(x + 7, y + 10), new(x + 12, y + 64),
            new(x + 24, y + 76), new(x + 3, y + 68), new(x - 18, y + 74), new(x - 6, y + 60)], Bark);
        canvas.Rect(x - 3, y + 20, slender ? 4 : 6, 40,
            slender ? new Color(159, 149, 105) : new Color(102, 79, 39));
        if (slender)
        {
            canvas.Rect(x - 3, y + 29, 4, 2, Bark);
            canvas.Rect(x - 2, y + 46, 3, 2, Bark);
        }
        canvas.Line(new(x - 1, y + 39), new(x - 26, y + 15), Bark, 6);
        canvas.Line(new(x + 2, y + 29), new(x + 30, y + 7), Bark, 5);
        for (var cluster = 0; cluster < 7; cluster++)
        {
            var n = Noise(seed * 13 + cluster, 77);
            var cx = x - (slender ? 23 : 43) + n % (slender ? 49 : 85);
            var cy = y - (slender ? 42 : 29) + n / 13 % (slender ? 65 : 43);
            var radius = (slender ? 19 : 27) + n % 11;
            Oval(canvas, cx, cy, radius, 17 + n % 9, DeepLeaf);
            Oval(canvas, cx - 4, cy - 4, radius - 4, 12 + n % 7, Leaf);
            canvas.Polygon([new(cx - radius + 9, cy - 4), new(cx - 14, cy - 16),
                new(cx - 9, cy - 9), new(cx + 2, cy - 19), new(cx + 8, cy - 10),
                new(cx + radius - 12, cy - 9), new(cx + 7, cy - 3)],
                slender ? new Color(119, 139, 56) : new Color(101, 129, 48));
            for (var fleck = 0; fleck < 5; fleck++)
                canvas.Rect(cx - 13 + fleck * 6, cy - 12 + (n + fleck) % 9, 3, 2, LightLeaf);
        }
    }

    public static void DrawHero(PixelCanvas canvas, int x, int y)
    {
        Oval(canvas, x, y + 20, 20, 5, new Color(39, 54, 26));
        canvas.Polygon([new(x - 10, y - 13), new(x + 8, y - 13), new(x + 17, y + 15),
            new(x + 9, y + 22), new(x - 14, y + 18), new(x - 19, y + 11)], new Color(39, 55, 27));
        canvas.Polygon([new(x - 9, y - 11), new(x + 7, y - 11), new(x + 12, y + 12),
            new(x + 4, y + 17), new(x - 12, y + 13)], new Color(96, 124, 48));
        canvas.Rect(x - 9, y - 5, 5, 19, new Color(140, 151, 67));
        canvas.Rect(x - 6, y + 12, 5, 10, Bark);
        canvas.Rect(x + 4, y + 13, 5, 9, Bark);
        canvas.Rect(x - 10, y + 20, 9, 3, new Color(98, 73, 39));
        canvas.Rect(x + 4, y + 20, 10, 3, new Color(98, 73, 39));
        Oval(canvas, x, y - 22, 11, 13, new Color(47, 59, 27));
        Oval(canvas, x, y - 21, 8, 10, new Color(183, 166, 99));
        canvas.Rect(x - 6, y - 25, 3, 3, new Color(49, 58, 27));
        canvas.Rect(x + 3, y - 25, 3, 3, new Color(49, 58, 27));
        canvas.Rect(x - 2, y - 18, 5, 2, new Color(118, 94, 48));
        canvas.Polygon([new(x - 10, y - 29), new(x - 16, y - 40), new(x - 4, y - 35),
            new(x - 2, y - 47), new(x + 5, y - 36), new(x + 15, y - 40), new(x + 10, y - 28)], DeepLeaf);
        canvas.Polygon([new(x - 9, y - 31), new(x - 11, y - 37), new(x - 2, y - 32),
            new(x - 1, y - 42), new(x + 4, y - 32), new(x + 10, y - 36), new(x + 8, y - 30)], LightLeaf);
        canvas.Line(new(x + 19, y - 34), new(x + 19, y + 23), new Color(78, 57, 28), 3);
        canvas.Line(new(x + 18, y - 31), new(x + 18, y + 21), new Color(170, 135, 66), 1);
        canvas.Polygon([new(x + 18, y - 34), new(x + 13, y - 44), new(x + 22, y - 39),
            new(x + 27, y - 44), new(x + 23, y - 33)], Leaf);
        canvas.Rect(x + 13, y - 1, 7, 4, new Color(181, 157, 91));
    }

    public static void DrawGuard(PixelCanvas canvas, int x, int y)
    {
        Oval(canvas, x, y + 20, 20, 5, new Color(40, 52, 30));
        canvas.Polygon([new(x - 10, y - 11), new(x + 9, y - 10), new(x + 16, y + 19),
            new(x + 6, y + 16), new(x - 13, y + 20)], new Color(100, 48, 34));
        canvas.Rect(x - 7, y - 9, 16, 25, new Color(85, 91, 81));
        canvas.Rect(x - 4, y - 9, 5, 20, new Color(159, 159, 130));
        canvas.Rect(x - 7, y + 12, 5, 11, new Color(54, 49, 38));
        canvas.Rect(x + 4, y + 12, 5, 11, new Color(54, 49, 38));
        Oval(canvas, x + 1, y - 24, 10, 12, new Color(51, 53, 44));
        Oval(canvas, x + 1, y - 23, 7, 9, new Color(188, 151, 96));
        canvas.Polygon([new(x - 10, y - 25), new(x - 7, y - 37), new(x + 5, y - 40),
            new(x + 12, y - 29), new(x + 10, y - 24)], new Color(67, 75, 70));
        canvas.Polygon([new(x - 6, y - 27), new(x - 4, y - 35), new(x + 4, y - 37),
            new(x + 8, y - 28)], new Color(161, 166, 140));
        canvas.Rect(x, y - 28, 3, 13, new Color(95, 105, 94));
        canvas.Rect(x + 4, y - 24, 3, 2, new Color(53, 49, 36));
        Oval(canvas, x - 12, y + 1, 11, 16, new Color(67, 48, 27));
        Oval(canvas, x - 12, y, 8, 13, new Color(159, 120, 57));
        Oval(canvas, x - 12, y, 5, 10, new Color(111, 60, 39));
        canvas.Rect(x - 13, y - 2, 3, 4, new Color(210, 184, 110));
        canvas.Line(new(x + 17, y + 9), new(x + 17, y - 24), new Color(54, 55, 45), 4);
        canvas.Line(new(x + 17, y + 6), new(x + 17, y - 22), new Color(199, 204, 177), 2);
        canvas.Rect(x + 12, y + 6, 11, 3, new Color(175, 141, 69));
    }

    public static void DrawRock(PixelCanvas canvas, int x, int y)
    {
        Oval(canvas, x, y + 17, 28, 6, new Color(47, 62, 31));
        canvas.Polygon([new(x - 27, y + 12), new(x - 21, y - 8), new(x - 8, y - 25),
            new(x + 13, y - 21), new(x + 25, y - 4), new(x + 26, y + 16), new(x + 3, y + 23)], new Color(71, 76, 61));
        canvas.Polygon([new(x - 20, y + 9), new(x - 17, y - 7), new(x - 7, y - 21),
            new(x + 10, y - 18), new(x + 18, y - 4), new(x + 1, y + 2)], new Color(149, 148, 113));
        canvas.Polygon([new(x + 1, y + 2), new(x + 18, y - 4), new(x + 22, y + 12),
            new(x + 3, y + 19)], new Color(105, 113, 87));
        canvas.Line(new(x - 7, y - 20), new(x - 2, y - 2), new Color(178, 174, 132), 2);
        canvas.Rect(x - 22, y + 9, 13, 4, new Color(87, 114, 45));
        canvas.Rect(x - 17, y + 6, 10, 3, new Color(117, 140, 58));
        canvas.Rect(x + 8, y + 15, 10, 3, new Color(82, 104, 42));
    }

    public static void DrawPortrait(PixelCanvas canvas, Rectangle bounds, bool flora)
    {
        canvas.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height,
            flora ? new Color(47, 62, 32) : new Color(79, 44, 31));
        var x = bounds.Center.X;
        var y = bounds.Y + 42;
        canvas.Polygon([new(bounds.X + 4, bounds.Bottom - 2), new(bounds.X + 9, bounds.Bottom - 17),
            new(x - 11, bounds.Bottom - 29), new(x + 12, bounds.Bottom - 25),
            new(bounds.Right - 7, bounds.Bottom - 13), new(bounds.Right - 3, bounds.Bottom - 2)],
            flora ? new Color(77, 93, 38) : new Color(121, 63, 40));
        canvas.Polygon([new(x - 16, y - 27), new(x + 13, y - 25), new(x + 22, y + 2),
            new(x + 12, y + 24), new(x - 11, y + 27), new(x - 24, y + 4)], DeepLeaf);
        canvas.Polygon([new(x - 10, y - 19), new(x + 10, y - 18), new(x + 15, y + 5),
            new(x + 7, y + 23), new(x - 8, y + 22), new(x - 16, y + 6)], new Color(167, 151, 94));
        canvas.Polygon([new(x + 5, y - 17), new(x + 10, y - 18), new(x + 15, y + 5),
            new(x + 7, y + 23), new(x + 4, y + 19)], new Color(119, 111, 65));
        canvas.Rect(x - 13, y - 7, 9, 3, new Color(66, 69, 34));
        canvas.Rect(x + 5, y - 6, 8, 3, new Color(66, 69, 34));
        canvas.Rect(x - 9, y - 4, 3, 2, new Color(210, 201, 133));
        canvas.Rect(x + 7, y - 3, 2, 2, new Color(210, 201, 133));
        canvas.Rect(x - 1, y - 2, 3, 11, new Color(114, 99, 57));
        canvas.Rect(x - 5, y + 15, 10, 2, new Color(92, 77, 41));
        if (flora)
        {
            canvas.Polygon([new(x - 23, y - 15), new(x - 30, y - 33), new(x - 13, y - 28),
                new(x - 8, y - 40), new(x + 5, y - 31), new(x + 22, y - 35),
                new(x + 19, y - 14)], DeepLeaf);
            canvas.Polygon([new(x - 19, y - 20), new(x - 23, y - 30), new(x - 11, y - 23),
                new(x - 6, y - 35), new(x + 2, y - 25), new(x + 15, y - 30), new(x + 13, y - 22)], Leaf);
            canvas.Line(new(x - 23, y - 29), new(x - 11, y - 23), LightLeaf, 1);
            canvas.Line(new(x - 6, y - 34), new(x + 1, y - 25), LightLeaf, 1);
        }
        else
        {
            canvas.Polygon([new(x - 23, y - 10), new(x - 17, y - 32), new(x + 8, y - 36),
                new(x + 22, y - 15)], new Color(126, 137, 119));
            canvas.Rect(x - 2, y - 12, 5, 18, new Color(111, 124, 111));
        }
    }

    public static void Oval(PixelCanvas canvas, int x, int y, int radiusX, int radiusY, Color color)
    {
        for (var row = -radiusY; row <= radiusY; row += 2)
        {
            var halfWidth = (int)(radiusX * Math.Sqrt(Math.Max(0, 1 - row * row / (double)(radiusY * radiusY))));
            canvas.Rect(x - halfWidth, y + row, halfWidth * 2 + 1, 2, color);
        }
    }

    private static int Noise(int x, int y)
    {
        var value = unchecked((uint)(x * 374761393 + y * 668265263));
        value = (value ^ (value >> 13)) * 1274126177;
        return (int)((value ^ (value >> 16)) & 0x7fffffff);
    }
}
