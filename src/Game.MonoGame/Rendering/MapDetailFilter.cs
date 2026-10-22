using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Load-time preview effects on map art only; all pixels remain premultiplied.</summary>
internal static class MapDetailFilter
{
    public static void ApplyTiles(Color[][] tiles, int tileWidth, int tileHeight, string style)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileHeight);
        if (tiles.Length != 4 || tiles.Any(tile => tile.Length != tileWidth * tileHeight))
            throw new ArgumentException("Expected four equal island quadrants in NW, NE, SW, SE order.");
        var width = tileWidth * 2;
        var height = tileHeight * 2;
        var whole = new Color[width * height];
        for (var tile = 0; tile < 4; tile++)
        for (var row = 0; row < tileHeight; row++)
            Array.Copy(tiles[tile], row * tileWidth, whole,
                (tile / 2 * tileHeight + row) * width + tile % 2 * tileWidth, tileWidth);
        Apply(whole, width, height, style);
        for (var tile = 0; tile < 4; tile++)
        for (var row = 0; row < tileHeight; row++)
            Array.Copy(whole, (tile / 2 * tileHeight + row) * width + tile % 2 * tileWidth,
                tiles[tile], row * tileWidth, tileWidth);
    }

    public static void Apply(Color[] pixels, int width, int height, string style)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != width * height || style is not ("sharpen" or "pixel"))
            throw new ArgumentException("Expected a complete image and sharpen or pixel style.");

        var block = style == "pixel" ? 2 : 1;
        var gridWidth = (width + block - 1) / block;
        var gridHeight = (height + block - 1) / block;
        var grid = new Color[gridWidth * gridHeight];
        for (var gy = 0; gy < gridHeight; gy++)
        for (var gx = 0; gx < gridWidth; gx++)
        {
            var red = 0; var green = 0; var blue = 0; var alpha = 0; var count = 0;
            for (var y = gy * block; y < Math.Min(height, (gy + 1) * block); y++)
            for (var x = gx * block; x < Math.Min(width, (gx + 1) * block); x++)
            {
                var colour = pixels[y * width + x];
                red += colour.R; green += colour.G; blue += colour.B; alpha += colour.A;
                count++;
            }
            grid[gy * gridWidth + gx] = new Color(red / count, green / count, blue / count, alpha / count);
        }

        var radius = style == "sharpen" ? 2 : 1;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var gx = x / block; var gy = y / block;
            var centre = grid[gy * gridWidth + gx];
            var left = grid[gy * gridWidth + Math.Max(0, gx - radius)];
            var right = grid[gy * gridWidth + Math.Min(gridWidth - 1, gx + radius)];
            var above = grid[Math.Max(0, gy - radius) * gridWidth + gx];
            var below = grid[Math.Min(gridHeight - 1, gy + radius) * gridWidth + gx];
            var alpha = pixels[y * width + x].A;
            pixels[y * width + x] = new Color(
                Enhance(centre.R, left.R, right.R, above.R, below.R, alpha),
                Enhance(centre.G, left.G, right.G, above.G, below.G, alpha),
                Enhance(centre.B, left.B, right.B, above.B, below.B, alpha), alpha);
        }
    }

    private static int Enhance(int centre, int left, int right, int above, int below, int alpha)
    {
        var blur = (centre * 4 + left + right + above + below) / 8;
        var difference = centre - blur;
        // Ignore tiny colour fluctuations and bound halos around strong edges.
        var detail = Math.Abs(difference) < 3 ? 0 : Math.Clamp(difference, -16, 16);
        return Math.Clamp(centre + detail, 0, alpha);
    }
}
