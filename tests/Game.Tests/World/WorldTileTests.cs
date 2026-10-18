using Game.Core.Geometry;
using Game.Core.World;
using Xunit;

namespace Game.Tests.World;

public sealed class WorldTileTests
{
    [Fact]
    public void Constructor_NegativeHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorldTile(new HexCoord(0, 0), height: -1));
    }
}
