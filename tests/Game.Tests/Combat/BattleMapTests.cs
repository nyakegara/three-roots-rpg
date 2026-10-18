using Game.Core.Combat;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat;

public sealed class BattleMapTests
{
    [Fact]
    public void GetTile_ImpassableTerrain_PreservesHeightAndCoordinate()
    {
        var coordinate = new HexCoord(-2, 1);
        var map = new BattleMap(new[] { new BattleTile(coordinate, height: 2, isPassable: false) });

        var tile = map.GetTile(coordinate);

        Assert.NotNull(tile);
        Assert.True(map.Contains(coordinate));
        Assert.Equal(coordinate, tile.Coordinate);
        Assert.Equal(2, tile.Height);
        Assert.False(tile.IsPassable);
        Assert.Null(map.GetTile(new HexCoord(-1, 1)));
    }

    [Fact]
    public void Constructor_SourceCollectionChanges_DoNotChangeMap()
    {
        var tiles = new List<BattleTile> { new(new HexCoord(0, 0)) };
        var map = new BattleMap(tiles);

        tiles.Clear();

        Assert.Single(map.Tiles);
        Assert.NotNull(map.GetTile(new HexCoord(0, 0)));
    }

    [Fact]
    public void Constructor_DuplicateCoordinate_ThrowsArgumentException()
    {
        var tiles = new[]
        {
            new BattleTile(new HexCoord(0, 0)),
            new BattleTile(new HexCoord(0, 0), height: 1)
        };

        Assert.Throws<ArgumentException>(() => new BattleMap(tiles));
    }

    [Fact]
    public void Constructor_NullCollection_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BattleMap(null!));
    }

    [Fact]
    public void Constructor_NullTile_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new BattleMap(new BattleTile[] { null! }));
    }

    [Fact]
    public void Tile_NegativeHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleTile(new HexCoord(0, 0), height: -1));
    }
}
