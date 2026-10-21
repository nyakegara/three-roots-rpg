namespace Game.Core.Navigation;

public sealed record AdventurePath(bool IsReachable, IReadOnlyList<MapPosition> Points, float Length)
{
    public static AdventurePath Unreachable { get; } = new(false, [], 0);
}

/// <summary>A* on an invisible fine grid; visibility smoothing removes grid-shaped travel.</summary>
public sealed class AdventurePathfinder
{
    private readonly AdventureSurface _surface;
    private readonly float _step;
    private readonly int _columns;
    private readonly int _rows;
    private readonly bool[] _walkable;
    private readonly Dictionary<long, bool> _edges = new();

    public AdventurePathfinder(AdventureSurface surface)
    {
        _surface = surface;
        _step = surface.Definition.CellSize;
        _columns = (int)MathF.Ceiling(surface.Definition.Width / _step) + 1;
        _rows = (int)MathF.Ceiling(surface.Definition.Height / _step) + 1;
        _walkable = new bool[_columns * _rows];
        for (var i = 0; i < _walkable.Length; i++) _walkable[i] = surface.IsWalkable(Point(i));
    }

    public AdventurePath FindPath(MapPosition start, MapPosition end)
    {
        if (!_surface.IsWalkable(start) || !_surface.IsWalkable(end)) return AdventurePath.Unreachable;
        if (_surface.IsSegmentWalkable(start, end)) return Build([start, end]);
        var first = FindConnector(start);
        var last = FindConnector(end);
        if (first < 0 || last < 0) return AdventurePath.Unreachable;
        var costs = new float[_walkable.Length];
        Array.Fill(costs, float.PositiveInfinity);
        var previous = new int[_walkable.Length];
        Array.Fill(previous, -1);
        var frontier = new PriorityQueue<int, float>();
        costs[first] = 0;
        frontier.Enqueue(first, 0);
        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (current == last)
            {
                var path = new List<MapPosition> { end };
                for (var node = last; node >= 0; node = previous[node]) path.Add(Point(node));
                path.Add(start);
                path.Reverse();
                return Build(Smooth(path));
            }
            if (priority > costs[current] + MapPosition.Distance(Point(current), Point(last)) + 0.01f) continue;
            var x = current % _columns;
            var y = current / _columns;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0 || x + dx < 0 || x + dx >= _columns || y + dy < 0 || y + dy >= _rows) continue;
                var next = (y + dy) * _columns + x + dx;
                if (!_walkable[next]) continue;
                var edgeKey = (long)Math.Min(current, next) * _walkable.Length + Math.Max(current, next);
                if (!_edges.TryGetValue(edgeKey, out var clear))
                    _edges[edgeKey] = clear = _surface.IsSegmentWalkable(Point(current), Point(next));
                if (!clear) continue;
                var nextCost = costs[current] + MapPosition.Distance(Point(current), Point(next));
                if (nextCost >= costs[next]) continue;
                costs[next] = nextCost;
                previous[next] = current;
                frontier.Enqueue(next, nextCost + MapPosition.Distance(Point(next), Point(last)));
            }
        }
        return AdventurePath.Unreachable;
    }

    public MapPosition FindNearestWalkable(MapPosition position)
    {
        if (_surface.IsWalkable(position)) return position;
        var candidates = Enumerable.Range(0, _walkable.Length).Where(i => _walkable[i]).ToArray();
        if (candidates.Length == 0) return _surface.Definition.Start;
        var best = candidates.MinBy(i => MapPosition.Distance(Point(i), position));
        return Point(best);
    }

    private int FindConnector(MapPosition position)
    {
        var x = (int)MathF.Round(position.X / _step);
        var y = (int)MathF.Round(position.Y / _step);
        var candidates = new List<int>();
        for (var dy = -3; dy <= 3; dy++)
        for (var dx = -3; dx <= 3; dx++)
            if (x + dx >= 0 && x + dx < _columns && y + dy >= 0 && y + dy < _rows)
            {
                var node = (y + dy) * _columns + x + dx;
                if (_walkable[node]) candidates.Add(node);
            }
        return candidates.OrderBy(i => MapPosition.Distance(position, Point(i)))
            .FirstOrDefault(i => _surface.IsSegmentWalkable(position, Point(i)), -1);
    }

    private MapPosition Point(int index) => new(index % _columns * _step, index / _columns * _step);

    private IReadOnlyList<MapPosition> Smooth(IReadOnlyList<MapPosition> path)
    {
        var result = new List<MapPosition> { path[0] };
        var current = 0;
        while (current < path.Count - 1)
        {
            var next = path.Count - 1;
            while (next > current + 1 && !_surface.IsSegmentWalkable(path[current], path[next])) next--;
            if (MapPosition.Distance(result[^1], path[next]) > 0.001f) result.Add(path[next]);
            current = next;
        }
        return result;
    }

    private static AdventurePath Build(IReadOnlyList<MapPosition> points) => new(true, points,
        points.Zip(points.Skip(1), MapPosition.Distance).Sum());
}
