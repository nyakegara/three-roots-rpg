using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Geometry;

public sealed class HexCoordTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(0, -1)]
    [InlineData(-1, 0)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    public void DistanceTo_AdjacentHexes_ReturnsOne(int q, int r)
    {
        var origin = new HexCoord(0, 0);
        var adjacent = new HexCoord(q, r);

        Assert.Equal(1, origin.DistanceTo(adjacent));
    }

    [Fact]
    public void GetNeighbors_ReturnsSixUniqueCoordinates()
    {
        var center = new HexCoord(2, -1);
        var neighbors = center.GetNeighbors();
        var expected = new[]
        {
            new HexCoord(3, -1),
            new HexCoord(3, -2),
            new HexCoord(2, -2),
            new HexCoord(1, -1),
            new HexCoord(1, 0),
            new HexCoord(2, 0)
        };

        Assert.Equal(6, neighbors.Count);
        Assert.Equal(6, neighbors.Distinct().Count());
        Assert.All(expected, coordinate => Assert.Contains(coordinate, neighbors));
    }

    [Theory]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MinValue, 0)]
    [InlineData(0, int.MaxValue)]
    [InlineData(0, int.MinValue)]
    public void GetNeighbors_CoordinateAtIntBoundary_ThrowsOverflowException(int q, int r)
    {
        var coordinate = new HexCoord(q, r);

        Assert.Throws<OverflowException>(() => coordinate.GetNeighbors());
    }

    [Theory]
    [InlineData(0, 0, 3, -2, 3)]
    [InlineData(2, -3, -3, 2, 5)]
    [InlineData(-4, 1, -4, 1, 0)]
    public void DistanceTo_UsesHexGridDistance(int startQ, int startR, int endQ, int endR, int expectedDistance)
    {
        var start = new HexCoord(startQ, startR);
        var end = new HexCoord(endQ, endR);

        Assert.Equal(expectedDistance, start.DistanceTo(end));
    }

    [Fact]
    public void DistanceTo_ResultExceedsIntRange_ThrowsOverflowException()
    {
        var start = new HexCoord(int.MinValue, int.MinValue);
        var end = new HexCoord(int.MaxValue, int.MaxValue);

        Assert.Throws<OverflowException>(() => start.DistanceTo(end));
    }
}
