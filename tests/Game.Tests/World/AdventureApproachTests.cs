using Game.Core.Navigation;
using Xunit;

namespace Game.Tests.World;

public sealed class AdventureApproachTests
{
    [Fact]
    public void NearbyLandmarkAcrossClosedBarrier_CannotBeInteractedWithFromThisSide()
    {
        var travel = new AdventureTravel(new(Map(gates: [new("key", Box(90, 0, 4, 120).Points)])), new(86, 60));
        Assert.False(travel.CanInteractWith(new(102, 60)));
        Assert.False(travel.SelectApproach(new(102, 60)));
        Assert.False(travel.Preview.IsReachable);
        Assert.False(travel.IsMoving);
    }

    [Fact]
    public void TwoClicksOnLandmark_StopAtSeparateNearbyPointWithoutTeleporting()
    {
        var travel = new AdventureTravel(new(Map()), new(20, 60), speed: 40);
        var npc = new MapPosition(180, 60);
        Assert.False(travel.SelectApproach(npc));
        Assert.Equal(new MapPosition(160, 60), travel.Destination);
        Assert.Equal(new MapPosition(20, 60), travel.Position);
        Assert.True(travel.SelectApproach(npc));
        travel.Advance(10);
        Assert.Equal(new MapPosition(160, 60), travel.Position);
        Assert.Equal(20, MapPosition.Distance(travel.Position, npc));
        Assert.True(travel.CanInteractWith(npc));
        Assert.False(travel.IsMoving);
    }

    [Fact]
    public void BlockedDirectApproach_SelectsReachableSideWithoutCuttingObstacle()
    {
        var surface = new AdventureSurface(Map(holes: [Box(156, 48, 16, 24)]));
        var travel = new AdventureTravel(surface, new(20, 60));
        var npc = new MapPosition(180, 60);
        travel.SelectApproach(npc);
        Assert.True(travel.Preview.IsReachable);
        Assert.NotEqual(new MapPosition(160, 60), travel.Destination);
        Assert.InRange(MapPosition.Distance(travel.Destination!.Value, npc), 19.99f, 20.01f);
        Assert.All(travel.Preview.Points.Zip(travel.Preview.Points.Skip(1)), segment =>
            Assert.True(surface.IsSegmentWalkable(segment.First, segment.Second)));
    }

    [Fact]
    public void StoryBarrier_CannotBeBypassedByChoosingLandmarkApproach()
    {
        var travel = new AdventureTravel(new(Map(gates: [new("key", Box(90, 0, 20, 120).Points)])), new(20, 60));
        Assert.False(travel.SelectApproach(new(180, 60)));
        Assert.False(travel.SelectApproach(new(180, 60)));
        Assert.False(travel.Preview.IsReachable);
        Assert.False(travel.IsMoving);
        Assert.Equal(new MapPosition(20, 60), travel.Position);
    }

    [Fact]
    public void Stop_CancelsOldRouteAndPreviewButKeepsExactCurrentPosition()
    {
        var travel = new AdventureTravel(new(Map()), new(20, 60), speed: 40);
        travel.SelectApproach(new(180, 60));
        travel.SelectApproach(new(180, 60));
        travel.Advance(0.3f);
        var before = travel.Position;
        travel.Stop();
        Assert.False(travel.Advance(10));
        Assert.Equal(before, travel.Position);
        Assert.Null(travel.Destination);
        Assert.Empty(travel.DisplayPath);
    }

    private static AdventureNavigation Map(IReadOnlyList<NavigationPolygon>? holes = null,
        IReadOnlyList<NavigationGate>? gates = null) => new(200, 120, 4, 2,
            [Box(4, 4, 192, 112)], holes ?? [], gates ?? [], new Dictionary<string, MapPosition>(), new(20, 60));
    private static NavigationPolygon Box(float x, float y, float width, float height) =>
        new([new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)]);
}
