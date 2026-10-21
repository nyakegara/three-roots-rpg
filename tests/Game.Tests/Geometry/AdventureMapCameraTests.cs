using System.Numerics;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Geometry;

public sealed class AdventureMapCameraTests
{
    [Fact]
    public void Update_CursorOutsideViewport_DoesNotPan()
    {
        var camera = new AdventureMapCamera(new Vector2(400, 300), new Vector2(1200, 900),
            new Vector2(100, 100));

        camera.Update(new Vector2(200, 350), 1);

        Assert.Equal(new Vector2(100, 100), camera.Position);
    }

    [Fact]
    public void Update_NonPositiveElapsedTime_DoesNotPan()
    {
        var camera = new AdventureMapCamera(new Vector2(400, 300), new Vector2(1200, 900),
            new Vector2(100, 100));

        camera.Update(new Vector2(0, 150), 0);

        Assert.Equal(new Vector2(100, 100), camera.Position);
    }

    [Fact]
    public void Pan_MapSmallerThanViewport_StaysAtOrigin()
    {
        var camera = new AdventureMapCamera(new Vector2(400, 300), new Vector2(200, 100),
            new Vector2(80, 50));

        camera.Update(new Vector2(0, 150), 1);

        Assert.Equal(Vector2.Zero, camera.Position);
    }

    [Fact]
    public void Update_PointerAtLeftEdge_PansLeftAtConfiguredSpeed()
    {
        var camera = new AdventureMapCamera(new Vector2(400, 300), new Vector2(1200, 900),
            new Vector2(100, 100));

        camera.Update(new Vector2(10, 150), 0.5f);

        Assert.Equal(new Vector2(0, 100), camera.Position);
    }
}
