using System.Numerics;

namespace Game.Core.Geometry;

/// <summary>Clamped edge-scrolling camera for the exploration viewport.</summary>
public sealed class AdventureMapCamera
{
    private const float EdgeBand = 20f;
    private const float PanSpeed = 360f;
    private readonly Vector2 _viewportSize;
    private readonly Vector2 _worldSize;

    public Vector2 Position { get; private set; }

    public AdventureMapCamera(Vector2 viewportSize, Vector2 worldSize, Vector2 initialPosition)
    {
        if (viewportSize.X <= 0 || viewportSize.Y <= 0) throw new ArgumentOutOfRangeException(nameof(viewportSize));
        if (worldSize.X < 0 || worldSize.Y < 0) throw new ArgumentOutOfRangeException(nameof(worldSize));
        _viewportSize = viewportSize;
        _worldSize = worldSize;
        Position = Clamp(initialPosition);
    }

    public void Update(Vector2 pointer, float elapsedSeconds)
    {
        if (elapsedSeconds <= 0 || pointer.X < 0 || pointer.Y < 0 ||
            pointer.X >= _viewportSize.X || pointer.Y >= _viewportSize.Y) return;

        var direction = Vector2.Zero;
        if (pointer.X < EdgeBand) direction.X = -1;
        else if (pointer.X >= _viewportSize.X - EdgeBand) direction.X = 1;
        if (pointer.Y < EdgeBand) direction.Y = -1;
        else if (pointer.Y >= _viewportSize.Y - EdgeBand) direction.Y = 1;
        if (direction != Vector2.Zero)
        {
            direction = Vector2.Normalize(direction);
            Position = Clamp(Position + direction * PanSpeed * elapsedSeconds);
        }
    }

    public Vector2 ToScreen(Vector2 worldPosition) => worldPosition - Position;
    public Vector2 ToWorld(Vector2 screenPosition) => screenPosition + Position;

    private Vector2 Clamp(Vector2 position) => new(
        Math.Clamp(position.X, 0, Math.Max(0, _worldSize.X - _viewportSize.X)),
        Math.Clamp(position.Y, 0, Math.Max(0, _worldSize.Y - _viewportSize.Y)));
}
