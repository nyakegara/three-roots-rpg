using System.Numerics;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Geometry;

public sealed class HexProjectionTests
{
    [Fact]
    public void Center_ProjectsAxialNeighborsToAdjacentHexes()
    {
        var projection = new HexProjection(40, 0.75f, 12, Vector2.Zero);

        var origin = projection.Center(new HexCoord(0, 0));
        var east = projection.Center(new HexCoord(1, 0));
        var northEast = projection.Center(new HexCoord(1, -1));

        Assert.Equal(MathF.Sqrt(3) * 40, east.X - origin.X, 3);
        Assert.Equal(0, east.Y - origin.Y, 3);
        Assert.Equal(MathF.Sqrt(3) * 40 / 2, northEast.X - origin.X, 3);
        Assert.Equal(-1.5f * 40 * 0.75f, northEast.Y - origin.Y, 3);
    }

    [Fact]
    public void Contains_IncludesHexCenterAndRejectsPointOutside()
    {
        var projection = new HexProjection(40, 0.75f, 12, new Vector2(100, 120));
        var coordinate = new HexCoord(2, 3);
        var center = projection.Center(coordinate, 1);

        Assert.True(projection.Contains(coordinate, center, 1));
        Assert.False(projection.Contains(coordinate, center + new Vector2(100, 0), 1));
    }

    [Fact]
    public void Center_RaisesTileByConfiguredElevationStep()
    {
        var projection = new HexProjection(40, 0.75f, 12, new Vector2(100, 120));

        var low = projection.Center(new HexCoord(1, 2), 0);
        var high = projection.Center(new HexCoord(1, 2), 2);

        Assert.Equal(24, low.Y - high.Y);
    }
}
