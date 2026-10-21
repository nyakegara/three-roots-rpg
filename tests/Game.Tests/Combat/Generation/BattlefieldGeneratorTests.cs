using Game.Core.Combat.Generation;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat.Generation;

public sealed class BattlefieldGeneratorTests
{
    private readonly BattlefieldGenerator _generator = new();

    [Theory]
    [InlineData(1729)]
    [InlineData(-1729)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Generate_SameSeed_ReproducesTerrainAndProps(int seed)
    {
        var first = _generator.Generate(seed);
        var second = _generator.Generate(seed);

        Assert.Equal(seed, first.Seed);
        Assert.Equal(first.Map.Tiles.Select(t => (t.Coordinate, t.Height, t.IsPassable, t.BlocksLineOfSight)),
            second.Map.Tiles.Select(t => (t.Coordinate, t.Height, t.IsPassable, t.BlocksLineOfSight)));
        Assert.Equal(first.Props, second.Props);
    }

    [Fact]
    public void Generate_DifferentSeeds_ProducesVariedHeightsAndProps()
    {
        var fields = Enumerable.Range(0, 20).Select(_generator.Generate).ToArray();
        Assert.True(fields.Select(f => string.Join(',', f.Map.Tiles.Select(t => t.Height))).Distinct().Count() > 10);
        Assert.True(fields.Select(f => string.Join(',', f.Props)).Distinct().Count() > 10);
    }

    [Fact]
    public void Generate_ManySeeds_PreservesTerrainAndDeploymentRules()
    {
        foreach (var seed in Seeds())
        {
            var field = _generator.Generate(seed);
            Assert.Equal(96, field.Map.Tiles.Count);
            Assert.Equal(Offset(2, 5), field.PlayerStart);
            Assert.Equal(Offset(9, 5), field.EnemyStart);
            Assert.Contains(field.Map.Tiles, t => t.Height == 2);
            foreach (var tile in field.Map.Tiles)
            {
                Assert.InRange(tile.Height, 0, 2);
                foreach (var neighbor in tile.Coordinate.GetNeighbors())
                    if (field.Map.GetTile(neighbor) is { } other)
                        Assert.InRange(Math.Abs(tile.Height - other.Height), 0, 1);
                var column = tile.Coordinate.Q + tile.Coordinate.R / 2;
                Assert.InRange(column, 0, 11);
                Assert.InRange(tile.Coordinate.R, 0, 7);
                if (column <= 2 || column >= 9)
                {
                    Assert.Equal(0, tile.Height);
                    Assert.True(tile.IsPassable);
                    Assert.DoesNotContain(field.Props, p => p.Coordinate == tile.Coordinate);
                }
            }
        }
    }

    [Fact]
    public void Generate_ManySeeds_PropsAgreeWithPassabilityAndLineOfSight()
    {
        foreach (var seed in Seeds())
        {
            var field = _generator.Generate(seed);
            Assert.Equal(field.Props.Count, field.Props.Select(p => p.Coordinate).Distinct().Count());
            Assert.InRange(field.Props.Count(p => p.Kind != BattlefieldPropKind.Shrub), 8, 12);
            Assert.Contains(field.Props, p => p.Kind == BattlefieldPropKind.Rock);
            Assert.Contains(field.Props, p => p.Kind == BattlefieldPropKind.FallenLog);
            Assert.Contains(field.Props, p => p.Kind == BattlefieldPropKind.Shrub);
            foreach (var tile in field.Map.Tiles)
            {
                var prop = field.Props.SingleOrDefault(p => p.Coordinate == tile.Coordinate);
                Assert.Equal(prop is null || prop.Kind == BattlefieldPropKind.Shrub, tile.IsPassable);
                Assert.Equal(prop?.Kind == BattlefieldPropKind.Rock, tile.BlocksLineOfSight);
            }
        }
    }

    [Fact]
    public void Generate_ManySeeds_AllPassableCellsAreReachable()
    {
        foreach (var seed in Seeds())
        {
            var field = _generator.Generate(seed);
            var visited = new HashSet<HexCoord> { field.PlayerStart };
            var queue = new Queue<HexCoord>();
            queue.Enqueue(field.PlayerStart);
            while (queue.TryDequeue(out var current))
                foreach (var neighbor in current.GetNeighbors())
                    if (field.Map.GetTile(neighbor)?.IsPassable == true && visited.Add(neighbor))
                        queue.Enqueue(neighbor);
            Assert.Equal(field.Map.Tiles.Count(t => t.IsPassable), visited.Count);
            Assert.Contains(field.EnemyStart, visited);
        }
    }

    private static IEnumerable<int> Seeds() => Enumerable.Range(-100, 200).Append(int.MinValue).Append(int.MaxValue);
    private static HexCoord Offset(int column, int row) => new(column - row / 2, row);
}
