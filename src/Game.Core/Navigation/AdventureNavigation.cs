using System.Numerics;
using System.Text.Json.Serialization;

namespace Game.Core.Navigation;

/// <summary>A point on the reference image, independent of camera and texture resolution.</summary>
public readonly record struct MapPosition(float X, float Y)
{
    [JsonIgnore] public Vector2 Vector => new(X, Y);
    [JsonIgnore] public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y);
    public static MapPosition From(Vector2 point) => new(point.X, point.Y);
    public static float Distance(MapPosition a, MapPosition b) => Vector2.Distance(a.Vector, b.Vector);
}

public sealed record NavigationPolygon(IReadOnlyList<MapPosition> Points);
public sealed record NavigationGate(string UnlockId, IReadOnlyList<MapPosition> Points);

/// <summary>Walkable regions, holes and story barriers loaded from the map JSON.</summary>
public sealed record AdventureNavigation(float Width, float Height, float CellSize, float AgentRadius,
    IReadOnlyList<NavigationPolygon> WalkableAreas, IReadOnlyList<NavigationPolygon> BlockedAreas,
    IReadOnlyList<NavigationGate> Gates, IReadOnlyDictionary<string, MapPosition> Points, MapPosition Start)
{
    public string? Validate()
    {
        if (!float.IsFinite(Width) || !float.IsFinite(Height) || Width <= 0 || Height <= 0 ||
            Width > 20000 || Height > 20000 || !float.IsFinite(CellSize) || CellSize < 2 ||
            !float.IsFinite(AgentRadius) || AgentRadius < 0 || AgentRadius > CellSize ||
            (double)Width / CellSize * Height / CellSize > 1_000_000)
            return "Navigation dimensions, cell size or agent radius are invalid.";
        if (WalkableAreas is null || WalkableAreas.Count == 0 || BlockedAreas is null || Gates is null || Points is null)
            return "Navigation must define walkable areas, obstacles, gates and point positions.";
        bool ValidPolygon(IReadOnlyList<MapPosition>? points) => points is { Count: >= 3 } &&
            points.All(p => p.IsFinite && p.X >= 0 && p.Y >= 0 && p.X <= Width && p.Y <= Height) &&
            MathF.Abs(points.Zip(points.Skip(1).Append(points[0]), (a, b) => a.X * b.Y - b.X * a.Y).Sum()) > 0.01f;
        if (WalkableAreas.Concat(BlockedAreas).Any(p => p is null || !ValidPolygon(p.Points)) ||
            Gates.Any(g => g is null || string.IsNullOrWhiteSpace(g.UnlockId) || !ValidPolygon(g.Points)))
            return "Navigation polygons need at least three finite points and a non-zero area.";
        if (!Start.IsFinite || Points.Any(p => string.IsNullOrWhiteSpace(p.Key) || !p.Value.IsFinite))
            return "Navigation start and landmarks must contain finite coordinates.";
        var surface = new AdventureSurface(this, _ => true);
        if (!surface.IsWalkable(Start) || Points.Values.Any(point => !surface.IsWalkable(point)))
            return "Navigation start and landmarks must be inside walkable areas and outside obstacles.";
        return null;
    }
}
