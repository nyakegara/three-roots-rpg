using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.MonoGame;

/// <summary>Integer raster primitives keep the preview crisp without content assets.</summary>
internal sealed class PixelCanvas : IDisposable
{
    private readonly Texture2D _pixel;
    private readonly SpriteBatch _batch;
    private readonly GraphicsDevice _device;

    public PixelCanvas(GraphicsDevice device, SpriteBatch batch)
    {
        _device = device;
        _batch = batch;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public IDisposable PushClip(Rectangle rectangle)
    {
        var previous = _device.ScissorRectangle;
        _device.ScissorRectangle = Rectangle.Intersect(previous, rectangle);
        return new ClipScope(_device, previous);
    }

    public void Rect(int x, int y, int width, int height, Color color)
    {
        if (width > 0 && height > 0)
            _batch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }

    public void Texture(Texture2D texture, Rectangle destination) =>
        _batch.Draw(texture, destination, Color.White);

    public void Frame(int x, int y, int width, int height, Color color, int thickness = 2)
    {
        Rect(x, y, width, thickness, color);
        Rect(x, y + height - thickness, width, thickness, color);
        Rect(x, y, thickness, height, color);
        Rect(x + width - thickness, y, thickness, height, color);
    }

    public void Line(Vector2 from, Vector2 to, Color color, int thickness = 1)
    {
        var x = (int)MathF.Round(from.X);
        var y = (int)MathF.Round(from.Y);
        var endX = (int)MathF.Round(to.X);
        var endY = (int)MathF.Round(to.Y);
        var dx = Math.Abs(endX - x);
        var dy = -Math.Abs(endY - y);
        var sx = x < endX ? 1 : -1;
        var sy = y < endY ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            Rect(x - thickness / 2, y - thickness / 2, thickness, thickness, color);
            if (x == endX && y == endY)
                break;
            var doubled = error * 2;
            if (doubled >= dy) { error += dy; x += sx; }
            if (doubled <= dx) { error += dx; y += sy; }
        }
    }

    public void Polygon(IReadOnlyList<Vector2> vertices, Color color)
    {
        var minY = (int)MathF.Ceiling(vertices.Min(p => p.Y));
        var maxY = (int)MathF.Floor(vertices.Max(p => p.Y));
        for (var y = minY; y <= maxY; y++)
        {
            var left = float.MaxValue;
            var right = float.MinValue;
            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Count];
                if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
                {
                    var x = a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                    left = MathF.Min(left, x);
                    right = MathF.Max(right, x);
                }
            }

            if (left <= right)
                Rect((int)MathF.Ceiling(left), y,
                    (int)MathF.Floor(right) - (int)MathF.Ceiling(left) + 1, 1, color);
        }
    }

    public void Outline(IReadOnlyList<Vector2> vertices, Color color, int thickness = 2)
    {
        for (var i = 0; i < vertices.Count; i++)
            Line(vertices[i], vertices[(i + 1) % vertices.Count], color, thickness);
    }

    public void Dispose() => _pixel.Dispose();

    private sealed class ClipScope(GraphicsDevice device, Rectangle previous) : IDisposable
    {
        public void Dispose() => device.ScissorRectangle = previous;
    }
}
