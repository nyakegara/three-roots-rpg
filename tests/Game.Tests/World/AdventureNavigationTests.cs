using Game.Core.Navigation;
using Xunit;

namespace Game.Tests.World;

public sealed class AdventureNavigationTests
{
    private static NavigationPolygon Box(float x, float y, float width, float height) =>
        new([new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)]);

    private static AdventureNavigation Map(IReadOnlyList<NavigationPolygon>? holes = null,
        IReadOnlyList<NavigationGate>? gates = null) =>
        new(200, 120, 4, 2, [Box(4, 4, 192, 112)], holes ?? [], gates ?? [],
            new Dictionary<string, MapPosition>(), new(20, 60));

    [Fact]
    public void Surface_RespectsBoundaryObstacleAndHeroRadius()
    {
        var surface = new AdventureSurface(Map([Box(80, 35, 40, 50)]));
        Assert.True(surface.IsWalkable(new(30.5f, 44.25f)));
        Assert.False(surface.IsWalkable(new(100, 60)));
        Assert.False(surface.IsWalkable(new(5, 60)));
        Assert.False(surface.IsWalkable(new(79, 60)));
        Assert.False(surface.IsSegmentWalkable(new(20, 60), new(180, 60)));
    }

    [Fact]
    public void Pathfinder_GoesAroundHoleAndKeepsExactDestination()
    {
        var surface = new AdventureSurface(Map([Box(80, 35, 40, 50)]));
        var path = new AdventurePathfinder(surface).FindPath(new(20, 60), new(177.25f, 61.75f));
        Assert.True(path.IsReachable);
        Assert.Equal(new MapPosition(177.25f, 61.75f), path.Points[^1]);
        Assert.True(path.Length > 157);
        Assert.All(path.Points.Zip(path.Points.Skip(1)), segment =>
            Assert.True(surface.IsSegmentWalkable(segment.First, segment.Second)));
    }

    [Fact]
    public void Gate_BlocksRouteUntilItsUnlock()
    {
        var map = Map(gates: [new("key", Box(90, 0, 20, 120).Points)]);
        Assert.False(new AdventurePathfinder(new(map)).FindPath(new(20, 60), new(180, 60)).IsReachable);
        Assert.True(new AdventurePathfinder(new(map, id => id == "key"))
            .FindPath(new(20, 60), new(180, 60)).IsReachable);
    }

    [Fact]
    public void Travel_PreviewsThenMovesOverTimeWithoutTeleporting()
    {
        var travel = new AdventureTravel(new(Map()), new(20, 60), speed: 40);
        Assert.False(travel.Select(new(180.25f, 60.5f)));
        Assert.Equal(new MapPosition(20, 60), travel.Position);
        Assert.True(travel.Select(new(180.25f, 60.5f)));
        Assert.Equal(new MapPosition(20, 60), travel.Position);
        Assert.False(travel.Advance(1));
        Assert.InRange(travel.Position.X, 59, 61);
        Assert.True(travel.Advance(10));
        Assert.Equal(new MapPosition(180.25f, 60.5f), travel.Position);
        Assert.False(travel.IsMoving);
    }

    [Fact]
    public void DifferentSecondClick_OnlyPreviewsItsNewRoute()
    {
        var travel = new AdventureTravel(new(Map()), new(20, 60));
        travel.Select(new(150, 60));
        Assert.False(travel.Select(new(160, 70)));
        Assert.False(travel.IsMoving);
        Assert.Equal(new MapPosition(160, 70), travel.Preview.Points[^1]);
        Assert.True(travel.Select(new(160, 70)));
    }

    [Fact]
    public void UnwalkableClick_DoesNotSendHeroToNearbyAllowedCell()
    {
        var travel = new AdventureTravel(new(Map([Box(80, 35, 40, 50)])), new(20, 60));
        Assert.False(travel.Select(new(100, 60)));
        Assert.False(travel.Select(new(100, 60)));
        Assert.False(travel.Preview.IsReachable);
        Assert.Equal(new MapPosition(20, 60), travel.Position);
    }
}
