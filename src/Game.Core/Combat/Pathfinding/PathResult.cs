using Game.Core.Geometry;

namespace Game.Core.Combat.Pathfinding;

/// <summary>A reachable path includes both endpoints. Its cost counts steps, not cells.</summary>
public sealed record PathResult(bool IsReachable, IReadOnlyList<HexCoord> Coordinates, int Cost);
