using Game.Core.Geometry;
using Game.Core.World;
using Xunit;

namespace Game.Tests.World;

public sealed class WorldMapTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 1)]
    public void WorldMap_OutsideCoordinate_ReturnsFalse(int q, int r)
    {
        var map = new WorldMap(new[]
        {
            new WorldTile(new HexCoord(0, 0)),
            new WorldTile(new HexCoord(2, 0))
        });

        Assert.False(map.Contains(new HexCoord(q, r)));
    }

    [Fact]
    public void Contains_ImpassableTileAtNegativeCoordinates_ReturnsTrue()
    {
        var map = new WorldMap(new[]
        {
            new WorldTile(new HexCoord(-2, 1), isPassable: false)
        });

        Assert.True(map.Contains(new HexCoord(-2, 1)));
    }

    [Fact]
    public void GetTile_KnownCoordinate_ReturnsMatchingTerrain()
    {
        var map = new WorldMap(new[]
        {
            new WorldTile(new HexCoord(0, 0)),
            new WorldTile(new HexCoord(1, -1), height: 2, isPassable: false)
        });

        var tile = map.GetTile(new HexCoord(1, -1));

        Assert.NotNull(tile);
        Assert.Equal(new HexCoord(1, -1), tile.Coordinate);
        Assert.Equal(2, tile.Height);
        Assert.False(tile.IsPassable);
    }

    [Fact]
    public void GetTile_MissingCoordinate_ReturnsNull()
    {
        var map = new WorldMap(new[] { new WorldTile(new HexCoord(0, 0)) });

        Assert.Null(map.GetTile(new HexCoord(1, 0)));
    }

    [Fact]
    public void EmptyMap_HasNoTiles()
    {
        var map = new WorldMap(Array.Empty<WorldTile>());

        Assert.Empty(map.Tiles);
        Assert.False(map.Contains(new HexCoord(0, 0)));
        Assert.Null(map.GetTile(new HexCoord(0, 0)));
    }

    [Fact]
    public void Tiles_EnumeratesEveryCell()
    {
        var map = new WorldMap(new[]
        {
            new WorldTile(new HexCoord(-1, 0)),
            new WorldTile(new HexCoord(0, 0), isPassable: false),
            new WorldTile(new HexCoord(1, 0), height: 1)
        });

        Assert.Equal(3, map.Tiles.Count);
        Assert.Contains(map.Tiles, tile => tile.Coordinate == new HexCoord(-1, 0));
        Assert.Contains(map.Tiles, tile => tile.Coordinate == new HexCoord(0, 0));
        Assert.Contains(map.Tiles, tile => tile.Coordinate == new HexCoord(1, 0));
    }

    [Fact]
    public void Constructor_DuplicateCoordinates_RejectsAmbiguousMap()
    {
        var tiles = new[]
        {
            new WorldTile(new HexCoord(1, -1)),
            new WorldTile(new HexCoord(1, -1), height: 2)
        };

        Assert.Throws<ArgumentException>(() => new WorldMap(tiles));
    }

    [Fact]
    public void Constructor_SourceCollectionChanges_DoNotChangeMap()
    {
        var tiles = new List<WorldTile> { new WorldTile(new HexCoord(0, 0)) };
        var map = new WorldMap(tiles);

        tiles.Clear();
        tiles.Add(new WorldTile(new HexCoord(1, 0)));

        Assert.Single(map.Tiles);
        Assert.NotNull(map.GetTile(new HexCoord(0, 0)));
        Assert.False(map.Contains(new HexCoord(1, 0)));
    }

    [Fact]
    public void Constructor_NullCollection_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new WorldMap(null!));
    }

    [Fact]
    public void Constructor_NullTile_ThrowsArgumentException()
    {
        var tiles = new WorldTile[] { new WorldTile(new HexCoord(0, 0)), null! };

        Assert.Throws<ArgumentException>(() => new WorldMap(tiles));
    }

    [Fact]
    public void MetadataConstructor_OmittedPointsOfInterest_UsesEmptyList()
    {
        var map = new WorldMap("test", "Test", 1, 1, new HexCoord(0, 0),
            new[] { new WorldTile(new HexCoord(0, 0)) });

        Assert.Empty(map.PointsOfInterest);
    }
}
