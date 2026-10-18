using Game.Core.Combat;
using Game.Core.Combat.Pathfinding;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat.Pathfinding;

public sealed class AStarPathfinderTests
{
    private readonly AStarPathfinder _pathfinder = new();

    [Fact]
    public void FindPath_OpenBoard_ReturnsShortestRoute()
    {
        var map = CreateBoard(5, 3);
        var start = new HexCoord(0, 1);
        var destination = new HexCoord(3, 1);

        var path = _pathfinder.FindPath(map, start, destination);

        Assert.Equal(3, path.Cost);
        AssertValidPath(map, path, start, destination);
    }

    [Fact]
    public void FindPath_BlockingTile_RoutesAroundObstacle()
    {
        var obstacle = new HexCoord(1, 1);
        var map = CreateBoard(4, 3, new HashSet<HexCoord> { obstacle });
        var start = new HexCoord(0, 1);
        var destination = new HexCoord(3, 1);

        var path = _pathfinder.FindPath(map, start, destination);

        Assert.Equal(4, path.Cost);
        Assert.DoesNotContain(obstacle, path.Coordinates);
        AssertValidPath(map, path, start, destination);
    }

    [Fact]
    public void FindPath_EnclosedDestination_ReturnsUnreachable()
    {
        var walls = new HashSet<HexCoord>
        {
            new(4, 1), new(4, 0), new(3, 0),
            new(2, 1), new(2, 2), new(3, 2)
        };
        var map = CreateBoard(5, 4, walls);

        var path = _pathfinder.FindPath(map, new HexCoord(0, 1), new HexCoord(3, 1));

        AssertUnreachable(path);
    }

    [Fact]
    public void FindPath_OccupiedCell_RoutesAroundIt()
    {
        var map = CreateBoard(4, 3);
        var start = new HexCoord(0, 1);
        var destination = new HexCoord(3, 1);
        var occupied = new HashSet<HexCoord> { new(1, 1) };

        var path = _pathfinder.FindPath(map, start, destination, occupied);

        Assert.Equal(4, path.Cost);
        Assert.DoesNotContain(new HexCoord(1, 1), path.Coordinates);
        AssertValidPath(map, path, start, destination);
    }

    [Fact]
    public void FindPath_OccupiedStart_CanLeaveItsCell()
    {
        var map = CreateBoard(4, 2);
        var start = new HexCoord(0, 0);
        var destination = new HexCoord(3, 0);

        var path = _pathfinder.FindPath(map, start, destination, new HashSet<HexCoord> { start });

        Assert.Equal(3, path.Cost);
        AssertValidPath(map, path, start, destination);
    }

    [Fact]
    public void FindPath_OccupiedDestination_ReturnsUnreachable()
    {
        var destination = new HexCoord(2, 0);
        var map = CreateBoard(3, 2);

        var path = _pathfinder.FindPath(map, new HexCoord(0, 0), destination,
            new HashSet<HexCoord> { destination });

        AssertUnreachable(path);
    }

    [Theory]
    [InlineData(-1, 0, 1, 0)]
    [InlineData(0, 0, 4, 0)]
    public void FindPath_MissingEndpoint_ReturnsUnreachable(int startQ, int startR, int endQ, int endR)
    {
        var map = CreateBoard(3, 2);

        var path = _pathfinder.FindPath(map, new HexCoord(startQ, startR), new HexCoord(endQ, endR));

        AssertUnreachable(path);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    public void FindPath_ImpassableEndpoint_ReturnsUnreachable(int q, int r)
    {
        var map = CreateBoard(3, 2, new HashSet<HexCoord> { new(q, r) });

        var path = _pathfinder.FindPath(map, new HexCoord(0, 0), new HexCoord(2, 0));

        AssertUnreachable(path);
    }

    [Fact]
    public void FindPath_StartEqualsDestination_ReturnsZeroCostSingleCell()
    {
        var map = CreateBoard(3, 2);
        var coordinate = new HexCoord(1, 1);

        var path = _pathfinder.FindPath(map, coordinate, coordinate, new HashSet<HexCoord> { coordinate });

        Assert.Equal(0, path.Cost);
        AssertValidPath(map, path, coordinate, coordinate);
    }

    [Fact]
    public void FindPath_GapInIrregularMap_ReturnsUnreachable()
    {
        var map = new BattleMap(new[]
        {
            new BattleTile(new HexCoord(0, 0)),
            new BattleTile(new HexCoord(2, 0))
        });

        AssertUnreachable(_pathfinder.FindPath(map, new HexCoord(0, 0), new HexCoord(2, 0)));
    }

    [Fact]
    public void FindPath_NegativeCoordinates_CanCrossOrigin()
    {
        var map = new BattleMap(Enumerable.Range(-2, 5)
            .Select(q => new BattleTile(new HexCoord(q, 0))));

        var path = _pathfinder.FindPath(map, new HexCoord(-2, 0), new HexCoord(2, 0));

        Assert.Equal(4, path.Cost);
        AssertValidPath(map, path, new HexCoord(-2, 0), new HexCoord(2, 0));
    }

    [Fact]
    public void FindPath_EqualCostRoutes_IsIndependentOfTileInsertionOrder()
    {
        var map = CreateBoard(4, 3);
        var reversedMap = new BattleMap(map.Tiles.Reverse());
        var start = new HexCoord(0, 1);
        var destination = new HexCoord(2, 0);

        var first = _pathfinder.FindPath(map, start, destination);
        var second = _pathfinder.FindPath(reversedMap, start, destination);

        Assert.Equal(2, first.Cost);
        Assert.Equal(first.Coordinates, second.Coordinates);
        AssertValidPath(map, first, start, destination);
    }

    [Fact]
    public void FindPath_AtIntBoundary_DoesNotWrapToOtherEdge()
    {
        var start = new HexCoord(int.MaxValue - 1, 0);
        var end = new HexCoord(int.MaxValue, 0);
        var map = new BattleMap(new[] { new BattleTile(start), new BattleTile(end) });

        var path = _pathfinder.FindPath(map, start, end);

        Assert.Equal(1, path.Cost);
        AssertValidPath(map, path, start, end);
    }

    [Fact]
    public void FindPath_WidelySeparatedCoordinates_ReturnsUnreachableWithoutOverflow()
    {
        var start = new HexCoord(int.MinValue, int.MinValue);
        var end = new HexCoord(int.MaxValue, int.MaxValue);
        var map = new BattleMap(new[] { new BattleTile(start), new BattleTile(end) });

        AssertUnreachable(_pathfinder.FindPath(map, start, end));
    }

    [Fact]
    public void FindPath_NullMap_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _pathfinder.FindPath(null!, new HexCoord(0, 0), new HexCoord(1, 0)));
    }

    private static BattleMap CreateBoard(int width, int height, IReadOnlySet<HexCoord>? walls = null)
    {
        var tiles = new List<BattleTile>();
        for (var r = 0; r < height; r++)
        for (var q = 0; q < width; q++)
        {
            var coordinate = new HexCoord(q, r);
            tiles.Add(new BattleTile(coordinate, isPassable: walls?.Contains(coordinate) != true));
        }

        return new BattleMap(tiles);
    }

    private static void AssertUnreachable(PathResult path)
    {
        Assert.False(path.IsReachable);
        Assert.Empty(path.Coordinates);
        Assert.Equal(0, path.Cost);
    }

    private static void AssertValidPath(BattleMap map, PathResult path, HexCoord start, HexCoord destination)
    {
        Assert.True(path.IsReachable);
        Assert.NotEmpty(path.Coordinates);
        Assert.Equal(start, path.Coordinates[0]);
        Assert.Equal(destination, path.Coordinates[^1]);
        Assert.Equal(path.Cost + 1, path.Coordinates.Count);
        Assert.All(path.Coordinates, cell => Assert.True(map.GetTile(cell)?.IsPassable));
        for (var index = 1; index < path.Coordinates.Count; index++)
        {
            Assert.Equal(1, path.Coordinates[index - 1].DistanceTo(path.Coordinates[index]));
        }
    }
}
