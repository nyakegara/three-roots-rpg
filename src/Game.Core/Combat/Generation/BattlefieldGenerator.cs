using Game.Core.Geometry;

namespace Game.Core.Combat.Generation;

/// <summary>Builds a small clearing from a seed, rejecting obstacles that split the field.</summary>
public sealed class BattlefieldGenerator
{
    private const int Columns = 12;
    private const int Rows = 8;

    public GeneratedBattlefield Generate(int seed)
    {
        var random = new Random(seed);
        var coordinates = new List<HexCoord>();
        for (var row = 0; row < Rows; row++)
            for (var column = 0; column < Columns; column++)
                coordinates.Add(Offset(column, row));

        var hills = new List<(HexCoord Center, int Radius)>();
        var hillCount = random.Next(2, 4);
        for (var i = 0; i < hillCount; i++)
            hills.Add((Offset(random.Next(4, 8), random.Next(1, 7)), random.Next(2, 4)));

        var heights = new Dictionary<HexCoord, int>();
        foreach (var coordinate in coordinates)
        {
            var column = coordinate.Q + coordinate.R / 2;
            var height = hills.Max(h => Math.Clamp(h.Radius - coordinate.DistanceTo(h.Center), 0, 2));
            // Distance to either deployment strip gives a gentle slope down to its flat ground.
            var deploymentLimit = Math.Max(0, Math.Min(column - 2, 9 - column));
            heights.Add(coordinate, Math.Min(height, deploymentLimit));
        }

        var candidates = coordinates.Where(c => IsInterior(c)).ToList();
        Shuffle(candidates, random);
        var blocked = new HashSet<HexCoord>();
        var props = new List<BattlefieldProp>();
        var obstacleCount = random.Next(8, 13);
        foreach (var coordinate in candidates)
        {
            blocked.Add(coordinate);
            if (!IsConnected(coordinates, blocked))
            {
                blocked.Remove(coordinate);
                continue;
            }
            // Alternate kinds so every field contains both cover and movement-only obstacles.
            var kind = props.Count % 2 == 0 ? BattlefieldPropKind.Rock : BattlefieldPropKind.FallenLog;
            props.Add(new BattlefieldProp(coordinate, kind));
            if (props.Count == obstacleCount)
                break;
        }

        var shrubCount = random.Next(4, 8);
        foreach (var coordinate in candidates.Where(c => !blocked.Contains(c)).Take(shrubCount))
            props.Add(new BattlefieldProp(coordinate, BattlefieldPropKind.Shrub));

        var rocks = props.Where(p => p.Kind == BattlefieldPropKind.Rock).Select(p => p.Coordinate).ToHashSet();
        var map = new BattleMap(coordinates.Select(c => new BattleTile(c, heights[c],
            isPassable: !blocked.Contains(c), blocksLineOfSight: rocks.Contains(c))));
        return new GeneratedBattlefield(seed, map, props, Offset(2, 5), Offset(9, 5));
    }

    private static HexCoord Offset(int column, int row) => new(column - row / 2, row);

    private static bool IsInterior(HexCoord coordinate)
    {
        var column = coordinate.Q + coordinate.R / 2;
        return column > 2 && column < 9;
    }

    private static void Shuffle(List<HexCoord> coordinates, Random random)
    {
        for (var i = coordinates.Count - 1; i > 0; i--)
        {
            var other = random.Next(i + 1);
            (coordinates[i], coordinates[other]) = (coordinates[other], coordinates[i]);
        }
    }

    private static bool IsConnected(List<HexCoord> coordinates, HashSet<HexCoord> blocked)
    {
        var passable = coordinates.Where(c => !blocked.Contains(c)).ToHashSet();
        var visited = new HashSet<HexCoord> { coordinates[0] };
        var queue = new Queue<HexCoord>();
        queue.Enqueue(coordinates[0]);
        while (queue.TryDequeue(out var current))
            foreach (var neighbor in current.GetNeighbors())
                if (passable.Contains(neighbor) && visited.Add(neighbor))
                    queue.Enqueue(neighbor);
        return visited.Count == passable.Count;
    }
}
