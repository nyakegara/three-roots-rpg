using System.Numerics;

namespace Game.Core.Geometry;

/// <summary>Projects axial hexes onto a flat screen plane without depending on a renderer.</summary>
public sealed class HexProjection
{
    private readonly float _radius;
    private readonly float _verticalCompression;
    private readonly float _elevationStep;
    private readonly Vector2 _origin;

    public HexProjection(float radius, float verticalCompression, float elevationStep, Vector2 origin)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        if (verticalCompression is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(verticalCompression));
        ArgumentOutOfRangeException.ThrowIfNegative(elevationStep);

        _radius = radius;
        _verticalCompression = verticalCompression;
        _elevationStep = elevationStep;
        _origin = origin;
    }

    public Vector2 Center(HexCoord coordinate, int height = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        return new Vector2(
            _origin.X + MathF.Sqrt(3) * _radius * (coordinate.Q + coordinate.R / 2f),
            _origin.Y + 1.5f * _radius * _verticalCompression * coordinate.R - height * _elevationStep);
    }

    public IReadOnlyList<Vector2> Corners(HexCoord coordinate, int height = 0, float inset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inset);
        if (inset >= _radius)
            throw new ArgumentOutOfRangeException(nameof(inset));

        var center = Center(coordinate, height);
        return Enumerable.Range(0, 6).Select(index =>
        {
            var angle = MathF.PI * (-0.5f + index / 3f);
            return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * _verticalCompression) *
                   (_radius - inset);
        }).ToArray();
    }

    public bool Contains(HexCoord coordinate, Vector2 point, int height = 0)
    {
        var corners = Corners(coordinate, height);
        var inside = false;
        for (var current = 0; current < corners.Count; current++)
        {
            var first = corners[current];
            var second = corners[(current + 1) % corners.Count];
            if (IsOnSegment(point, first, second))
                return true;

            if ((first.Y > point.Y) != (second.Y > point.Y) &&
                point.X < (second.X - first.X) * (point.Y - first.Y) /
                (second.Y - first.Y) + first.X)
                inside = !inside;
        }

        return inside;
    }

    private static bool IsOnSegment(Vector2 point, Vector2 first, Vector2 second)
    {
        var edge = second - first;
        var relative = point - first;
        var cross = edge.X * relative.Y - edge.Y * relative.X;
        if (MathF.Abs(cross) > 0.01f)
            return false;

        var dot = Vector2.Dot(relative, edge);
        return dot >= 0 && dot <= edge.LengthSquared();
    }
}
