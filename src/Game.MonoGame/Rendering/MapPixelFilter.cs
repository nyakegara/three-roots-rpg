using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Optional map-art experiment, applied once at load; sprites and UI stay untouched.</summary>
internal static class MapPixelFilter
{
    public static void Apply(Color[] pixels, int width, int height, int screenPixelSize,
        int offsetX = 0, int offsetY = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != width * height || screenPixelSize is < 1 or > 2)
            throw new ArgumentException("Expected a complete image and a 1 or 2 pixel map style.");
        // Current map textures contain two source pixels per game pixel.
        var block = screenPixelSize * 2;
        var source = (Color[])pixels.Clone();
        for (var y = 0; y < height; y++)
        {
            var sampleY = Math.Clamp((y + offsetY) / block * block + block / 2 - offsetY, 0, height - 1);
            for (var x = 0; x < width; x++)
            {
                var sampleX = Math.Clamp((x + offsetX) / block * block + block / 2 - offsetX, 0, width - 1);
                var colour = source[sampleY * width + sampleX];
                pixels[y * width + x] = new Color(Math.Min(Step(colour.R), colour.A),
                    Math.Min(Step(colour.G), colour.A), Math.Min(Step(colour.B), colour.A), colour.A);
            }
        }
    }

    private static int Step(byte channel) => (int)MathF.Round(MathF.Round(channel * 31f / 255) * 255 / 31);
}
