using Game.Core.Geometry;
using Game.Core.World;
using Game.MonoGame.Rendering;
using Xunit;

namespace Game.Tests;

public sealed class AdventurePointVisibilityTests
{
    [Fact]
    public void CompletedQuestMarkerDisappearsButItsPersistentGuideCanRemain()
    {
        var point = Point("pasture", "quest");
        Assert.True(AdventureMapRenderer.IsPointVisible(point, true, Actors(), new HashSet<string> { point.Id }));
        Assert.False(AdventureMapRenderer.IsPointVisible(point, true, Actors(), new HashSet<string>()));
        Assert.True(AdventureMapRenderer.IsPointVisible(point, true, Actors(point.Id), new HashSet<string>()));
    }

    [Fact]
    public void AtSharedQuestAndBattleLocation_OnlyCurrentBattleRemainsAfterChoice()
    {
        var quest = Point("pasture-request", "quest");
        var battle = Point("pasture-battle", "encounter");
        var actors = Actors(battle.Id);
        Assert.False(AdventureMapRenderer.IsPointVisible(quest, true, actors, new HashSet<string>()));
        Assert.True(AdventureMapRenderer.IsPointVisible(battle, true, actors, new HashSet<string>()));
        Assert.False(AdventureMapRenderer.IsPointVisible(battle, true, Actors(), new HashSet<string>()));
    }

    [Fact]
    public void BuildingsAndStandaloneMapPreviewsAreRetainedWithoutQuestState()
    {
        Assert.True(AdventureMapRenderer.IsPointVisible(Point("camp", "recruitment"), true, Actors(), new HashSet<string>()));
        Assert.True(AdventureMapRenderer.IsPointVisible(Point("quest", "quest"), true, Actors(), null));
        Assert.True(AdventureMapRenderer.IsPointVisible(Point("legacy", "encounter"), false, Actors(), null));
        Assert.False(AdventureMapRenderer.IsPointVisible(Point("gate", "gate"), true, Actors(), null));
    }

    private static WorldPointOfInterest Point(string id, string type) => new(id, type, id, new HexCoord(0, 0));
    private static IReadOnlyDictionary<string, string> Actors(string? id = null) => id is null
        ? new Dictionary<string, string>() : new Dictionary<string, string> { [id] = "unit.idle" };
}
