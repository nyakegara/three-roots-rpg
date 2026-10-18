using Game.Core.Combat.Pathfinding;
using Game.Core.Geometry;

namespace Game.Core.Combat;

/// <summary>Select a destination to preview it; select it again to confirm the shown path.</summary>
public sealed class MovementSelection
{
    private readonly BattleMap _map;
    private readonly IReadOnlySet<HexCoord>? _occupied;
    private readonly AStarPathfinder _pathfinder = new();

    public HexCoord Position { get; private set; }
    public HexCoord? Destination { get; private set; }
    public PathResult Path { get; private set; } = new(false, Array.Empty<HexCoord>(), 0);

    public MovementSelection(BattleMap map, HexCoord start, IReadOnlySet<HexCoord>? occupied = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.GetTile(start)?.IsPassable != true)
        {
            throw new ArgumentException("The starting cell must exist and be passable.", nameof(start));
        }

        _map = map;
        _occupied = occupied;
        Position = start;
    }

    /// <returns>True only when a second selection confirms movement.</returns>
    public bool Select(HexCoord destination)
    {
        var path = _pathfinder.FindPath(_map, Position, destination, _occupied);
        if (Destination == destination && Path.IsReachable && path.IsReachable && path.Cost > 0 &&
            Path.Coordinates.SequenceEqual(path.Coordinates))
        {
            Position = destination;
            Destination = null;
            Path = new PathResult(false, Array.Empty<HexCoord>(), 0);
            return true;
        }

        // A different target or changed obstacle layout requires previewing the new route first.
        Destination = destination;
        Path = path;
        return false;
    }
}
