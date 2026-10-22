using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Trims atlas padding while preserving one scale and body pivot for a complete action.</summary>
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

    public static Rectangle FitToReference(Rectangle visibleSource, Rectangle referenceSource,
        Rectangle targetBounds, bool flip = false, int cellWidth = 64, int cellHeight = 80)
    {
        if (visibleSource.Width <= 0 || visibleSource.Height <= 0 || referenceSource.Width <= 0 ||
            referenceSource.Height <= 0 || targetBounds.Width <= 0 || targetBounds.Height <= 0)
            return targetBounds;

        var scaleX = targetBounds.Width / (float)referenceSource.Width;
        var scaleY = targetBounds.Height / (float)referenceSource.Height;
        var pivotX = referenceSource.X % cellWidth + referenceSource.Width / 2f;
        var groundY = referenceSource.Y % cellHeight + referenceSource.Height;
        var localX = visibleSource.X % cellWidth;
        var localY = visibleSource.Y % cellHeight;
        var offsetX = flip ? pivotX - localX - visibleSource.Width : localX - pivotX;
        var width = Math.Max(1, (int)MathF.Round(visibleSource.Width * scaleX));
        var height = Math.Max(1, (int)MathF.Round(visibleSource.Height * scaleY));
        return new Rectangle(targetBounds.X + (int)MathF.Round(targetBounds.Width / 2f + offsetX * scaleX),
            targetBounds.Bottom - (int)MathF.Round((groundY - localY) * scaleY), width, height);
    }
}
