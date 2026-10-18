using Game.Core.Geometry;

namespace Game.Core.Combat.Pathfinding;

/// <summary>A* with unit movement costs and a hex-distance heuristic.</summary>
public sealed class AStarPathfinder
{
    private static readonly IReadOnlyList<HexCoord> Directions = new HexCoord(0, 0).GetNeighbors();

    public PathResult FindPath(BattleMap map, HexCoord start, HexCoord destination,
        IReadOnlySet<HexCoord>? occupied = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.GetTile(start)?.IsPassable != true || map.GetTile(destination)?.IsPassable != true ||
            (destination != start && occupied?.Contains(destination) == true))
        {
            return new PathResult(false, Array.Empty<HexCoord>(), 0);
        }

        // The sequence number breaks equal-score ties in a stable direction order.
        var frontier = new PriorityQueue<HexCoord, (long Estimate, long Sequence)>();
        var costs = new Dictionary<HexCoord, int> { [start] = 0 };
        var previous = new Dictionary<HexCoord, HexCoord>();
        var visited = new HashSet<HexCoord>();
        long sequence = 0;
        frontier.Enqueue(start, (EstimateDistance(start, destination), sequence++));

        while (frontier.TryDequeue(out var current, out _))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == destination)
            {
                return RestorePath(previous, destination, costs[destination]);
            }

            foreach (var neighbor in GetRepresentableNeighbors(current))
            {
                if (visited.Contains(neighbor) || map.GetTile(neighbor)?.IsPassable != true ||
                    occupied?.Contains(neighbor) == true)
                {
                    continue;
                }

                var newCost = costs[current] + 1;
                if (costs.TryGetValue(neighbor, out var oldCost) && oldCost <= newCost)
                {
                    continue;
                }

                costs[neighbor] = newCost;
                previous[neighbor] = current;
                frontier.Enqueue(neighbor, (newCost + EstimateDistance(neighbor, destination), sequence++));
            }
        }

        return new PathResult(false, Array.Empty<HexCoord>(), 0);
    }

    private static PathResult RestorePath(Dictionary<HexCoord, HexCoord> previous, HexCoord end, int cost)
    {
        var path = new List<HexCoord> { end };
        while (previous.TryGetValue(end, out var parent))
        {
            path.Add(parent);
            end = parent;
        }

        path.Reverse();
        return new PathResult(true, path.AsReadOnly(), cost);
    }

    private static IEnumerable<HexCoord> GetRepresentableNeighbors(HexCoord coordinate)
    {
        foreach (var direction in Directions)
        {
            var q = (long)coordinate.Q + direction.Q;
            var r = (long)coordinate.R + direction.R;
            if (q is >= int.MinValue and <= int.MaxValue && r is >= int.MinValue and <= int.MaxValue)
            {
                yield return new HexCoord((int)q, (int)r);
            }
        }
    }

    private static long EstimateDistance(HexCoord start, HexCoord end)
    {
        // Same cube-distance metric as HexCoord.DistanceTo, kept as long for far-apart components.
        var q = (long)start.Q - end.Q;
        var r = (long)start.R - end.R;
        return Math.Max(Math.Abs(q), Math.Max(Math.Abs(r), Math.Abs(q + r)));
    }
}
