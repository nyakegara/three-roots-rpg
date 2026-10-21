using System.Numerics;
using Game.Core.Content;
using Game.Core.Geometry;
using Game.Core.World;
using Xunit;

namespace Game.Tests.Geometry;

public sealed class AdventureMapProjectionTests
{
    [Fact]
    public void Project_UsesTwoToOneIsometricBasisAndElevation()
    {
        var map = CreateMap(2, 3);
        var projection = new AdventureMapProjection(map, 64, 32, 8);

        var origin = projection.Project(new HexCoord(0, 0));
        Assert.Equal(new Vector2(32, 16), projection.Project(new HexCoord(1, 0)) - origin);
        Assert.Equal(new Vector2(-32, 16), projection.Project(new HexCoord(0, 1)) - origin);
        Assert.Equal(origin - new Vector2(0, 16), projection.Project(new HexCoord(0, 0), 2));
    }

    [Fact]
    public void TryPick_EmptyMapSpace_ReturnsFalse()
    {
        var map = CreateMap(3, 3, new HexCoord(1, 1));
        var projection = new AdventureMapProjection(map, 64, 32, 8);

        Assert.False(projection.TryPick(new Vector2(1000, 1000), out _));
    }

    [Fact]
    public void TryPick_ProjectAndCameraOffset_SelectsOriginalTile()
    {
        var map = CreateMap(4, 4);
        var projection = new AdventureMapProjection(map, 64, 32, 8);
        var camera = new AdventureMapCamera(new Vector2(320, 200), projection.WorldSize,
            new Vector2(80, 40));
        var target = new HexCoord(2, 1);
        var screenPosition = camera.ToScreen(projection.Project(target, 1));

        Assert.True(projection.TryPick(camera.ToWorld(screenPosition), out var selected));
        Assert.Equal(target, selected);
    }

    [Theory]
    [InlineData("guide-dialogue", 1475, 214)]
    [InlineData("north-gate", 1114, 389)]
    [InlineData("mountain-outpost", 839, 501)]
    [InlineData("mushroom-outpost", 690, 725)]
    [InlineData("final-landing", 233, 461)]
    public void FloraLandmark_ProjectsNearPaintedLocation(string pointId, int expectedX, int expectedY)
    {
        var map = LoadFloraMap();
        var projection = new AdventureMapProjection(map, 88, 40, 5, 2);
        var point = map.PointsOfInterest.Single(candidate => candidate.Id == pointId);
        var tile = map.GetTile(point.Coordinate)!;
        var world = projection.ProjectPoint(point);
        var artX = world.X / projection.WorldSize.X * 1862;
        var artY = world.Y / projection.WorldSize.Y * 845;

        Assert.InRange(artX, expectedX - 25, expectedX + 25);
        Assert.InRange(artY, expectedY - 25, expectedY + 25);
    }

    [Fact]
    public void FloraWalkableTile_CanBePickedAtItsPaintedCenter()
    {
        var map = LoadFloraMap();
        var projection = new AdventureMapProjection(map, 88, 40, 5, 2);

        foreach (var tile in map.Tiles.Where(candidate => candidate.IsPassable))
        {
            Assert.True(projection.TryPick(projection.Project(tile.Coordinate, tile.Height), out var picked));
            Assert.Equal(tile.Coordinate, picked);
        }
    }

    private static WorldMap LoadFloraMap()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "maps", "flora-01.json");
        var result = new MapLoader().Load(path);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        return result.Map!;
    }

    private static WorldMap CreateMap(int width, int height, params HexCoord[] missing)
    {
        var absent = missing.ToHashSet();
        return new WorldMap("test", "Test", width, height, null,
            from r in Enumerable.Range(0, height)
            from q in Enumerable.Range(0, width)
            let coordinate = new HexCoord(q, r)
            where !absent.Contains(coordinate)
            select new WorldTile(coordinate));
    }
}
