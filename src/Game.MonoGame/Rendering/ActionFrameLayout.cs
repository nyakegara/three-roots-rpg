using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.MonoGame.Rendering;

/// <summary>Trims transparent atlas padding so every animation frame keeps the same visual height.</summary>
internal static class ActionFrameLayout
{
    private const byte VisibleAlphaCutoff = 128;

    public static Rectangle FindVisibleBounds(Color[] pixels, int textureWidth, Rectangle frame)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureWidth);
        if (pixels.Length % textureWidth != 0)
            throw new ArgumentException("Pixel data must contain complete rows.", nameof(pixels));

        var textureHeight = pixels.Length / textureWidth;
        var clipped = Rectangle.Intersect(frame, new Rectangle(0, 0, textureWidth, textureHeight));
        if (clipped.Width <= 0 || clipped.Height <= 0) return Rectangle.Empty;

        var left = clipped.Right;
        var top = clipped.Bottom;
        var right = clipped.Left - 1;
        var bottom = clipped.Top - 1;
        for (var y = clipped.Top; y < clipped.Bottom; y++)
        for (var x = clipped.Left; x < clipped.Right; x++)
        {
            if (pixels[y * textureWidth + x].A < VisibleAlphaCutoff) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        return right < left || bottom < top
            ? Rectangle.Empty
            : new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    public static Rectangle FitToHeight(Rectangle visibleSource, Rectangle targetBounds)
    {
        if (visibleSource.Width <= 0 || visibleSource.Height <= 0 || targetBounds.Height <= 0)
            return targetBounds;

        var scale = targetBounds.Height / (float)visibleSource.Height;
        var width = Math.Max(1, (int)MathF.Round(visibleSource.Width * scale));
        return new Rectangle(targetBounds.Center.X - width / 2,
            targetBounds.Bottom - targetBounds.Height, width, targetBounds.Height);
    }
}
