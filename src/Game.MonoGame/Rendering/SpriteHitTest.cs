using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Matches point-sampled, cropped drawing without selecting transparent padding.</summary>
internal static class SpriteHitTest
{
    public static bool IsVisible(byte[] alpha, int textureWidth, Rectangle source,
        Rectangle destination, Point point, bool flip = false)
    {
        ArgumentNullException.ThrowIfNull(alpha);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureWidth);
        if (!destination.Contains(point) || source.Width <= 0 || source.Height <= 0) return false;
        // PointClamp samples at the destination pixel's centre, including when reducing art.
        var u = (point.X - destination.X + 0.5) / destination.Width;
        var x = Math.Min(source.Width - 1, (int)((flip ? 1 - u : u) * source.Width));
        var y = Math.Min(source.Height - 1, (int)((point.Y - destination.Y + 0.5) * source.Height / destination.Height));
        x += source.X;
        y += source.Y;
        return x >= 0 && x < textureWidth && y >= 0 && y < alpha.Length / textureWidth &&
            alpha[y * textureWidth + x] >= 128;
    }
}
