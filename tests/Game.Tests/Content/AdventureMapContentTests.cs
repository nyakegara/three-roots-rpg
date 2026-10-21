using Game.Core.Content;
using Game.Core.Navigation;
using Game.Core.Geometry;
using Game.MonoGame.Rendering;
using Xunit;

namespace Game.Tests.Content;

public sealed class AdventureMapContentTests
{
    [Fact]
    public void SavedApproachPosition_RestoresBesideLandmarkWithItsLogicalQuestAnchor()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        var nav = definition.Map.Navigation!;
        var point = definition.Map.PointsOfInterest.Single(point => point.Id == "pasture-request");
        var travel = new AdventureTravel(new(nav, campaign.IsUnlocked), nav.Start);
        travel.SelectApproach(nav.Points[point.Id]);
        Assert.True(travel.SelectApproach(nav.Points[point.Id]));
        travel.Advance(100);
        travel.Stop();
        Assert.NotEqual(nav.Points[point.Id], travel.Position);
        var save = campaign.CreateSave(definition.Id, definition.Map.Id, point.Coordinate) with
            { AdventurePosition = travel.Position };
        Assert.True(definition.CreateGameState().TryRestoreSave(save, definition.Id, definition.PlayerFactionId,
            definition.Map, units, out var error), error);
    }

    [Fact]
    public void FloraLandmarks_HaveReachableStandingSpotsIncludingOffsetGuide()
    {
        var result = new MapLoader().Load(Path.Combine(AppContext.BaseDirectory, "content", "maps", "flora-01.json"));
        var map = result.Map!;
        var nav = map.Navigation!;
        var surface = new AdventureSurface(nav, _ => true);
        var travel = new AdventureTravel(surface, nav.Start);
        var projection = new AdventureMapProjection(map, 88, 40, 5, 2);
        foreach (var point in map.PointsOfInterest.Where(point => point.Type != "gate"))
        {
            var landmark = projection.ToArt(projection.ProjectPoint(point) + AdventureMapRenderer.IdleActorOffset(point.Id));
            travel.Stop();
            travel.SelectApproach(landmark);
            Assert.True(travel.Preview.IsReachable, $"No standing spot beside '{point.Id}'.");
            Assert.InRange(MapPosition.Distance(travel.Destination!.Value, landmark), 19.99f, 20.01f);
        }
    }

    private static AdventureNavigation Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "maps", "flora-01.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var navigation = System.Text.Json.JsonSerializer.Deserialize<AdventureNavigation>(
            document.RootElement.GetProperty("navigation").GetRawText(),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var surface = new AdventureSurface(navigation, _ => true);
        var blocked = navigation.Points.Where(point => !surface.IsWalkable(point.Value)).ToArray();
        Assert.True(blocked.Length == 0, "Blocked landmarks: " + string.Join("; ", blocked));
        var result = new MapLoader().Load(path);
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
    public void FloraMap_NorthRequiresChoiceBattleButMountainApproachIsOpen()
    {
        var nav = Load();
        var finder = new AdventurePathfinder(new(nav));
        Assert.True(finder.FindPath(nav.Start, nav.Points["pasture-request"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["burned-forest"]).IsReachable);
        Assert.True(finder.FindPath(nav.Start, nav.Points["mountain-outpost"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["artifact-ash-guard"]).IsReachable);
        finder = new(new(nav, id => id == "north-gate"));
        Assert.True(finder.FindPath(nav.Start, nav.Points["burned-forest"]).IsReachable);
        Assert.True(finder.FindPath(nav.Start, nav.Points["mountain-outpost"]).IsReachable);
        finder = new(new(nav, id => id is "north-gate" or "burnt-cleared"));
        Assert.True(finder.FindPath(nav.Start, nav.Points["mountain-outpost"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["mushroom-outpost"]).IsReachable);
        finder = new(new(nav, id => id == "mountain-route"));
        Assert.True(finder.FindPath(nav.Start, nav.Points["mushroom-outpost"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["burned-forest"]).IsReachable);
        Assert.False(finder.FindPath(nav.Start, nav.Points["final-landing"]).IsReachable);
    }

    [Theory]
    [InlineData("burned-forest", .590f, .334f)]
    [InlineData("mountain-outpost", .468f, .532f)]
    [InlineData("optional-patrol", .577f, .467f)]
    [InlineData("plants-pasture-battle", .711f, .648f)]
    [InlineData("mad-forest-plants", .717f, .337f)]
    [InlineData("mushroom-outpost", .425f, .662f)]
    [InlineData("artifact-ash-guard", .290f, .630f)]
    [InlineData("artifact-spore-guard", .339f, .444f)]
    [InlineData("final-landing", .188f, .501f)]
    public void MarkedBattlesMatchTheNineReferenceLocations(string id, float x, float y)
    {
        var nav = Load();
        // Percentages refer to the complete 1280x720 screenshot; the artwork
        // occupies 110,123,1060,462 inside it.
        var expected = new MapPosition((x * 1280 - 110) / 1060 * nav.Width,
            (y * 720 - 123) / 462 * nav.Height);
        // The southern plants stand just above the tree obstacle under the marker.
        Assert.InRange(MapPosition.Distance(nav.Points[id], expected), 0,
            id == "plants-pasture-battle" ? 16 : .02f);
    }

    [Fact]
    public void NineMarkedBattlesHaveDistinctRolesAndInquisitorDebutsAtFinalLanding()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var loaded = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json"));
        Assert.True(loaded.Success, string.Join("; ", loaded.Errors));
        var encounters = loaded.Definition!.Encounters.ToDictionary(e => e.Id);
        Assert.Equal(10, encounters.Count); // Nine red markers plus the refusal fight at the peasants.
        Assert.Equal(3, encounters["burned-forest"].EnemyArmy.Count);
        Assert.Equal(2, encounters["mountain-outpost"].EnemyArmy.Single(t => t.UnitTypeId == "human.banner-knight").Count);
        Assert.Equal(2, encounters["mountain-outpost"].EnemyArmy.Single(t => t.UnitTypeId == "human.order-hunter").Count);
        Assert.Single(encounters["optional-patrol"].EnemyArmy);
        Assert.All(encounters["optional-patrol"].EnemyArmy, t => Assert.Equal("human.militia", t.UnitTypeId));
        Assert.All(encounters["plants-pasture-battle"].EnemyArmy, t => Assert.StartsWith("flora.", t.UnitTypeId));
        Assert.All(encounters["mad-forest-plants"].EnemyArmy, t => Assert.StartsWith("flora.", t.UnitTypeId));
        Assert.Equal(4, encounters["mushroom-outpost"].EnemyArmy.Count);
        Assert.All(encounters.Values.Where(e => e.Id != "final-landing"), e =>
            Assert.DoesNotContain(e.EnemyArmy, t => t.UnitTypeId == "human.inquisitor"));
        Assert.Equal(5, encounters["final-landing"].EnemyArmy.Count);
        Assert.Contains(encounters["final-landing"].EnemyArmy, t => t.UnitTypeId == "human.inquisitor");
        Assert.Equal(loaded.Definition.Map.PointsOfInterest.Single(p => p.Id == "pasture-request").Coordinate,
            loaded.Definition.Map.PointsOfInterest.Single(p => p.Id == "farmers-pasture-battle").Coordinate);
        Assert.NotEqual(loaded.Definition.Map.PointsOfInterest.Single(p => p.Id == "pasture-request").Coordinate,
            loaded.Definition.Map.PointsOfInterest.Single(p => p.Id == "plants-pasture-battle").Coordinate);
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
