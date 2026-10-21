using System.Numerics;

namespace Game.Core.Navigation;

/// <summary>Checks the hero's footprint and route segments against authored shapes.</summary>
public sealed class AdventureSurface
{
    public AdventureNavigation Definition { get; }
    private readonly Shape[] _obstacles;
    private readonly Shape[] _walkable;

    public AdventureSurface(AdventureNavigation definition, Func<string, bool>? isUnlocked = null)
    {
        Definition = definition;
        _walkable = definition.WalkableAreas.Select(area => new Shape(area.Points)).ToArray();
        _obstacles = definition.BlockedAreas.Select(area => area.Points)
            .Concat(definition.Gates.Where(gate => isUnlocked?.Invoke(gate.UnlockId) != true)
                .Select(gate => gate.Points)).Select(points => new Shape(points)).ToArray();
    }

    public bool IsWalkable(MapPosition point)
    {
        if (!ContainsCenter(point)) return false;
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4;
            if (!ContainsCenter(new(point.X + MathF.Cos(angle) * Definition.AgentRadius,
                    point.Y + MathF.Sin(angle) * Definition.AgentRadius))) return false;
        }
        return true;
    }

    public bool IsSegmentWalkable(MapPosition from, MapPosition to)
    {
        if (!from.IsFinite || !to.IsFinite) return false;
        var steps = Math.Max(1, (int)MathF.Ceiling(MapPosition.Distance(from, to) / 2));
        for (var i = 0; i <= steps; i++)
            if (!IsWalkable(MapPosition.From(Vector2.Lerp(from.Vector, to.Vector, i / (float)steps)))) return false;
        return true;
    }

    private bool ContainsCenter(MapPosition point) => point.IsFinite &&
        point.X >= 0 && point.Y >= 0 && point.X <= Definition.Width && point.Y <= Definition.Height &&
        _walkable.Any(shape => shape.ContainsPoint(point)) &&
        !_obstacles.Any(shape => shape.ContainsPoint(point));

    private sealed class Shape(IReadOnlyList<MapPosition> points)
    {
        private readonly float _minX = points.Min(p => p.X);
        private readonly float _maxX = points.Max(p => p.X);
        private readonly float _minY = points.Min(p => p.Y);
        private readonly float _maxY = points.Max(p => p.Y);
        public bool ContainsPoint(MapPosition point) => point.X >= _minX && point.X <= _maxX &&
            point.Y >= _minY && point.Y <= _maxY && Contains(points, point);
    }

    public static bool Contains(IReadOnlyList<MapPosition> polygon, MapPosition point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[j];
            var b = polygon[i];
            var edge = b.Vector - a.Vector;
            var lengthSquared = edge.LengthSquared();
            var t = lengthSquared > 0 ? Math.Clamp(Vector2.Dot(point.Vector - a.Vector, edge) / lengthSquared, 0, 1) : 0;
            if (Vector2.DistanceSquared(point.Vector, a.Vector + edge * t) < 0.0001f) return true;
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
}
