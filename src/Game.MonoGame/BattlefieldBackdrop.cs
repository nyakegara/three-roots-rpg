using Microsoft.Xna.Framework;

namespace Game.MonoGame;

/// <summary>Seeded horizon details layered over the shared clearing palette.</summary>
internal static class BattlefieldBackdrop
{
    public static void Draw(PixelCanvas canvas, int seed, float nightBlend)
    {
        var sky = Color.Lerp(new Color(126, 151, 166), new Color(29, 41, 69), nightBlend);
        FantasyArt.DrawLandscape(canvas, sky);

        // Small treeline silhouettes vary between generated encounters while staying behind the field.
        var random = new Random(seed);
        for (var i = 0; i < 6; i++)
        {
            var x = 45 + i * 225 + random.Next(-35, 36);
            var height = 25 + random.Next(0, 22);
            var width = 16 + random.Next(0, 9);
            canvas.Rect(x - 2, 80 - height, 4, height + 16, new Color(64, 54, 31));
            canvas.Polygon([new(x - width, 80 - height / 2), new(x, 80 - height - 12),
                new(x + width, 80 - height / 2), new(x + width - 6, 80 - height / 2 + 5),
                new(x - width + 6, 80 - height / 2 + 5)], new Color(43, 69, 34));
            canvas.Polygon([new(x - width + 3, 80 - height / 2 - 3), new(x, 80 - height - 12),
                new(x + 3, 80 - height / 2 - 1)], new Color(74, 99, 42));
        }

        var sunOpacity = Math.Clamp(1f - nightBlend, 0f, 1f);
        var moonOpacity = Math.Clamp(nightBlend, 0f, 1f);
        if (sunOpacity > 0f)
        {
            var sunlight = new Color(228, 203, 131) * (0.86f * sunOpacity);
            canvas.Rect(1085, 30, 16, 16, sunlight);
            canvas.Rect(1089, 26, 8, 24, sunlight);
        }
        if (moonOpacity > 0f)
        {
            var moonlight = new Color(214, 226, 244) * (0.92f * moonOpacity);
            var moonHorizon = Color.Lerp(sky, new Color(95, 112, 92), 0.42f);
            var moonShadow = Color.Lerp(sky, moonHorizon, 28f / 88f);
            canvas.Rect(1086, 28, 18, 18, moonlight);
            canvas.Rect(1094, 24, 10, 25, moonlight);
            canvas.Rect(1091, 26, 7, 15, moonShadow);

            for (var i = 0; i < 9; i++)
            {
                var x = 75 + i * 117 + random.Next(-22, 23);
                var y = 14 + random.Next(0, 51);
                if (x is > 1075 and < 1120)
                    continue;
                canvas.Rect(x, y, 2, 2, new Color(197, 214, 238) * (0.9f * moonOpacity));
            }
        }
    }
}
