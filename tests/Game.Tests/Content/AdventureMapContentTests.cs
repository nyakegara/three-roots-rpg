using Game.Core.Content;
using Game.Core.Navigation;
using Xunit;

namespace Game.Tests.Content;

public sealed class AdventureMapContentTests
{
    private static AdventureNavigation Load()
    {
        var result = new MapLoader().Load(Path.Combine(AppContext.BaseDirectory, "content", "maps", "flora-01.json"));
        Assert.True(result.Success, string.Join("; ", result.Errors));
        return Assert.IsType<AdventureNavigation>(result.Map!.Navigation);
    }

    [Fact]
    public void FloraMap_ContainsAuthoredWalkRegionsAndAllLandmarks()
    {
        var nav = Load();
        Assert.Null(nav.Validate());
        Assert.True(nav.WalkableAreas.Count >= 5);
        Assert.True(nav.BlockedAreas.Count >= 15);
        Assert.Equal(1862, nav.Width);
        Assert.True(new AdventureSurface(nav).IsWalkable(nav.Start));
    }

    [Fact]
    public void FloraMap_AllLandmarksAreReachableAfterUnlockingRoutes()
    {
        var nav = Load();
        var surface = new AdventureSurface(nav, _ => true);
        var finder = new AdventurePathfinder(surface);
        foreach (var point in nav.Points)
        {
            var path = finder.FindPath(nav.Start, point.Value);
            Assert.True(path.IsReachable, $"Cannot reach '{point.Key}' at {point.Value}.");
            Assert.All(path.Points.Zip(path.Points.Skip(1)), s =>
                Assert.True(surface.IsSegmentWalkable(s.First, s.Second)));
        }
    }

    [Fact]
    public void FloraMap_PastureIsOpenButNorthAndMountainsRequireStoryProgress()
    {
        var nav = Load();
        var finder = new AdventurePathfinder(new(nav));
        Assert.True(finder.FindPath(nav.Start, nav.Points["pasture-request"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["burned-forest"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["mountain-outpost"]).IsReachable);
        finder = new(new(nav, id => id == "north-gate"));
        Assert.True(finder.FindPath(nav.Start, nav.Points["burned-forest"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["mountain-outpost"]).IsReachable);
        finder = new(new(nav, id => id is "north-gate" or "burnt-cleared"));
        Assert.True(finder.FindPath(nav.Start, nav.Points["mountain-outpost"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["mushroom-outpost"]).IsReachable);
    }

    [Theory]
    [InlineData(1320.5f, 545.25f, true)]
    [InlineData(1100, 247, false)]
    [InlineData(1572, 220, false)]
    public void Restore_ValidatesExactPositionBeforeMutatingHero(float x, float y, bool expected)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        var save = campaign.CreateSave(definition.Id, definition.Map.Id, definition.Map.StartPosition!.Value) with
            { AdventurePosition = new(x, y), Gold = 123 };
        var restored = definition.CreateGameState();
        Assert.Equal(expected, restored.TryRestoreSave(save, definition.Id, definition.PlayerFactionId,
            definition.Map, units, out _));
        Assert.Equal(expected ? 123 : campaign.Hero.Gold, restored.Hero.Gold);
    }

    [Fact]
    public void Save_CannotAddPeasantsBeforeTheirRecruitmentIsUnlocked()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        var save = campaign.CreateSave(definition.Id, definition.Map.Id, definition.Map.StartPosition!.Value) with
            { Army = [new Game.Core.Persistence.SavedTroopStack("human.militia", 1)] };
        Assert.False(campaign.TryRestoreSave(save, definition.Id, definition.PlayerFactionId, definition.Map, units, out _));
    }

    [Fact]
    public void Save_WithLegitimatelyHiredPeasantsRestoresAfterHelpingThem()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        Assert.True(campaign.ApplyChoice("pasture-choice", "help").Success);
        Assert.True(campaign.TryRestoreProgress(new Game.Core.Campaigns.CampaignProgressSnapshot(["plants-pasture-battle"], false)));
        var site = campaign.RecruitmentSites.Single(s => s.Id == "peasant-recruitment");
        Assert.True(new Game.Core.Economy.EconomyRules().TryRecruit(campaign.Hero, site, "human.militia", 1).Success);
        var save = campaign.CreateSave(definition.Id, definition.Map.Id, definition.Map.StartPosition!.Value);
        var restored = definition.CreateGameState();
        Assert.True(restored.TryRestoreSave(save, definition.Id, definition.PlayerFactionId, definition.Map, units,
            out var error), error);
        Assert.Contains(restored.Hero.Army.Stacks, s => s.UnitTypeId == "human.militia");
    }

    [Theory]
    [InlineData(1572, 220)]
    [InlineData(459, 437)]
    [InlineData(1408, 660)]
    [InlineData(1540, 647)]
    [InlineData(400, 250)]
    [InlineData(500, 100)]
    public void FloraMap_GreenMarkedObstaclesAndOutsideAreasAreNotWalkable(float x, float y) =>
        Assert.False(new AdventureSurface(Load(), _ => true).IsWalkable(new(x, y)));
}
