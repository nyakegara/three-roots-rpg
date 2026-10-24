using Game.Core.Content;
using Game.Core.Economy;
using Xunit;

namespace Game.Tests.Content;

public sealed class CampaignLoaderTests : IDisposable
{
    private readonly List<string> _temporaryDirectories = [];
    [Fact]
    public void Load_UnknownRecruitmentUnit_ReturnsFileDiagnostic()
    {
        var folder = WriteContent("missing.flora.unit", "quest-one", duplicateTile: false);
        var result = LoadTemporaryCampaign(folder);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("missing.flora.unit", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_QuestReferencesMissingMapPoint_ReturnsDiagnostic()
    {
        var folder = WriteContent("flora.scout", "absent-point", duplicateTile: false);
        var result = LoadTemporaryCampaign(folder);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("missing quest point", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_MapWithDuplicateCoordinates_ReturnsMapDiagnostic()
    {
        var folder = WriteContent("flora.scout", "quest-one", duplicateTile: true);
        var result = LoadTemporaryCampaign(folder);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("duplicate tile coordinate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_MapFileOutsideMapDirectory_ReturnsDiagnostic()
    {
        var folder = WriteContent("flora.scout", "quest-one", duplicateTile: false, mapFile: "../units/units.json");
        var result = LoadTemporaryCampaign(folder);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("map directory", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("absent-point", "site-one", "flora.scout", "missing recruitment point")]
    [InlineData("site-point", "site-one", "missing.unit", "unknown unitId")]
    [InlineData("site-point", "site-one", "human.guard", "player faction")]
    public void Load_InvalidRecruitmentSiteLinkOrOffer_ReturnsDiagnostic(
        string pointId, string siteId, string unitId, string expectedError)
    {
        var folder = WriteSiteContent(pointId, siteId, unitId);

        var result = LoadTemporaryCampaign(folder);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_DuplicateRecruitmentSiteId_ReturnsDiagnostic()
    {
        var folder = WriteSiteContent("site-point", "site-one", "flora.scout",
            extraSite: "{\"id\":\"site-one\",\"pointOfInterestId\":\"site-point-two\",\"offers\":[{\"unitId\":\"flora.scout\",\"startingStock\":2}]}",
            extraPoint: "{\"id\":\"site-point-two\",\"type\":\"recruitment\",\"name\":\"Second\",\"q\":0,\"r\":0,\"recruitmentSiteId\":\"site-one\"}");

        var result = LoadTemporaryCampaign(folder);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("duplicate recruitment site", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("[]", "at least one offer")]
    [InlineData("[{\"unitId\":\"flora.scout\",\"startingStock\":0}]", "positive startingStock")]
    [InlineData("[{\"unitId\":\"flora.scout\",\"startingStock\":2},{\"unitId\":\"flora.scout\",\"startingStock\":3}]", "duplicate offer")]
    public void Load_InvalidRecruitmentOffers_ReturnsDiagnostic(string offers, string expectedError)
    {
        var folder = WriteSiteContent("site-point", "site-one", "flora.scout", offers: offers);

        var result = LoadTemporaryCampaign(folder);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_UnlinkedRecruitmentMapPoint_ReturnsDiagnostic()
    {
        var folder = WriteSiteContent("site-point", "site-one", "flora.scout",
            extraPoint: "{\"id\":\"orphan\",\"type\":\"recruitment\",\"name\":\"Orphan\",\"q\":0,\"r\":0,\"recruitmentSiteId\":\"missing-site\"}");

        var result = LoadTemporaryCampaign(folder);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("unlinked recruitment point", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_ContentDirectory_ReturnsThreePlayableCampaignDefinitions()
    {
        var contentDirectory = Path.Combine(AppContext.BaseDirectory, "content");
        var unitResult = UnitCatalog.Load(Path.Combine(contentDirectory, "units", "units.json"));
        Assert.True(unitResult.Success, string.Join(Environment.NewLine, unitResult.Errors));
        var loader = new CampaignLoader(Assert.IsType<UnitCatalog>(unitResult.Catalog));
        var campaigns = new[] { "flora-01.json", "human-01.json", "undead-01.json" }
            .Select(file => loader.Load(Path.Combine(contentDirectory, "campaigns", file)))
            .ToArray();

        Assert.All(campaigns, result => Assert.True(result.Success,
            string.Join(Environment.NewLine, result.Errors)));
        Assert.Equal(3, campaigns.Length);
        Assert.Equal(4, campaigns[0].Definition!.Quests.Count);
        Assert.All(campaigns.Skip(1), result => Assert.Equal(3, result.Definition!.Quests.Count));
        Assert.All(campaigns.Skip(1), result => Assert.All(result.Definition!.Quests,
            quest => Assert.Equal(2, quest.Quest.Choices.Count)));
        Assert.Empty(campaigns[0].Definition!.Recruitment);
        Assert.All(campaigns.Skip(1), result => Assert.Equal(5, result.Definition!.Recruitment.Count));
        Assert.Equal(3, campaigns.Select(result => result.Definition!.Map.Id).Distinct().Count());
        Assert.Equal(new[] { "flora", "human", "undead" }, campaigns
            .Select(result => result.Definition!.PlayerFactionId).Order(StringComparer.Ordinal).ToArray());

        var first = campaigns[0].Definition!;
        Assert.Equal(4, first.RecruitmentSites.Count);
        Assert.Equal(6, first.RecruitmentSites.Sum(site => site.Offers.Count));
        Assert.Equal(new[] { "flora-tier-three", "flora-tier-two", "peasant-recruitment", "starting-clearing" },
            first.RecruitmentSites.Select(site => site.Id).Order(StringComparer.Ordinal));
        Assert.All(first.RecruitmentSites, site => Assert.Contains(first.Map.PointsOfInterest,
            point => point.Id == site.PointOfInterestId && point.RecruitmentSiteId == site.Id &&
                     point.Type == "recruitment" && first.Map.GetTile(point.Coordinate)!.IsPassable));
        Assert.Equal(new[]
        {
            ("starting-clearing", "flora.thorn-guard", 18),
            ("starting-clearing", "flora.sprout", 8),
            ("peasant-recruitment", "human.militia", 12),
            ("flora-tier-two", "flora.ember-bloom", 5),
            ("flora-tier-two", "flora.pod-slinger", 5),
            ("flora-tier-three", "flora.root-warden", 3)
        }, first.RecruitmentSites.SelectMany(site => site.Offers.Select(offer =>
            (site.Id, offer.UnitId, offer.StartingStock))));
        Assert.Equal(new[] { ("flora.thorn-guard", 12), ("flora.sprout", 3) },
            first.StartingArmy.Select(stack => (stack.UnitTypeId, stack.Count)));
        Assert.Null(first.Map.Tiles.Single(tile => tile.Coordinate == new Game.Core.Geometry.HexCoord(12, -8)).RequiredUnlock);
        Assert.Null(first.Map.Tiles.Single(tile => tile.Coordinate == new Game.Core.Geometry.HexCoord(13, -8)).RequiredUnlock);
        Assert.Equal("north-gate", first.Map.Tiles.Single(tile => tile.Coordinate == new Game.Core.Geometry.HexCoord(10, -16)).RequiredUnlock);
        Assert.Equal(new Game.Core.Geometry.HexCoord(12, -12),
            first.Map.PointsOfInterest.Single(point => point.Id == "north-gate").Coordinate);
        Assert.Empty(campaigns[1].Definition!.RecruitmentSites);
        Assert.Empty(campaigns[2].Definition!.RecruitmentSites);
        Assert.Equal(10, first.Encounters.Count);
        Assert.Contains(first.Encounters, encounter => encounter.Id == "mountain-outpost" &&
            encounter.BattlefieldId == "mountain-outpost");
        Assert.Contains(first.Encounters, encounter => encounter.Id == "final-landing" &&
            new[] { "human.militia", "human.longbow", "human.banner-knight", "human.order-hunter", "human.inquisitor" }
                .All(unitId => encounter.EnemyArmy.Any(troop => troop.UnitTypeId == unitId)));
        Assert.Empty(campaigns[1].Definition!.Encounters);
        Assert.Empty(campaigns[2].Definition!.Encounters);
        var state = first.CreateGameState();
        Assert.Equal(first.HeroName, state.Hero.Name);
        Assert.Equal(first.StartingGold, state.Hero.Gold);
        Assert.Equal("flora", state.Hero.FactionId);
        var grove = state.RecruitmentSites.Single(site => site.Id == "starting-clearing");
        var pasture = state.RecruitmentSites.Single(site => site.Id == "peasant-recruitment");
        Assert.Equal("flora", grove.FactionId);
        Assert.Equal("human", pasture.FactionId);
        state.Hero.ChangeReputation(2);
        var economy = new EconomyRules();
        Assert.Equal(12, economy.GetAdjustedUnitCost(state.Hero, grove, grove.GetOffer("flora.thorn-guard")!));
        Assert.Equal(16, economy.GetAdjustedUnitCost(state.Hero, pasture, pasture.GetOffer("human.militia")!));
        Assert.Equal(4, state.Quests.Count);
        Assert.Equal(4, state.RecruitmentSites.Count);
        Assert.Equal(first.Encounters.Count, state.Encounters.Count);
        Assert.Equal(6, state.GetRecruitmentStockSnapshot().Count);
        Assert.Equal(new[] { 3, 5, 5, 8, 12, 18 }, state.GetRecruitmentStockSnapshot()
            .Select(stock => stock.RemainingStock).Order().ToArray());
    }

    [Fact]
    public void UnitCatalog_ContainsFiveBalancedUnitTypesPerFaction()
    {
        var result = UnitCatalog.Load(Path.Combine(AppContext.BaseDirectory, "content", "units", "units.json"));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(17, result.Catalog!.Units.Count);
        Assert.Equal(5, result.Catalog.Units.Count(unit => unit.FactionId == "flora"));
        Assert.Equal(7, result.Catalog.Units.Count(unit => unit.FactionId == "human"));
        Assert.Equal(5, result.Catalog.Units.Count(unit => unit.FactionId == "undead"));
        Assert.All(result.Catalog.Units, unit =>
        {
            Assert.InRange(unit.HitPoints, 5, 100);
            Assert.InRange(unit.Damage, 1, 30);
            Assert.InRange(unit.Speed, 1, 20);
            Assert.InRange(unit.Movement, 1, 10);
            Assert.InRange(unit.Range, 1, 8);
            Assert.InRange(unit.RecruitmentCost, 1, 500);
        });
    }

    public void Dispose()
    {
        foreach (var directory in _temporaryDirectories) Directory.Delete(directory, recursive: true);
    }

    private CampaignLoadResult LoadTemporaryCampaign(string folder)
    {
        var content = Path.Combine(folder, "content");
        var units = UnitCatalog.Load(Path.Combine(content, "units", "units.json"));
        Assert.True(units.Success, string.Join(Environment.NewLine, units.Errors));
        return new CampaignLoader(units.Catalog!).Load(Path.Combine(content, "campaigns", "campaign.json"));
    }

    private string WriteContent(string unitId, string pointId, bool duplicateTile, string mapFile = "../maps/map.json")
    {
        var folder = Path.Combine(Path.GetTempPath(), $"campaign-{Guid.NewGuid():N}");
        var unitsDirectory = Path.Combine(folder, "content", "units");
        var mapsDirectory = Path.Combine(folder, "content", "maps");
        var campaignsDirectory = Path.Combine(folder, "content", "campaigns");
        Directory.CreateDirectory(unitsDirectory);
        Directory.CreateDirectory(mapsDirectory);
        Directory.CreateDirectory(campaignsDirectory);
        _temporaryDirectories.Add(folder);
        File.WriteAllText(Path.Combine(unitsDirectory, "units.json"), """
        {"units":[{"id":"flora.scout","factionId":"flora","name":"Разведчик","hitPoints":8,"damage":3,"speed":7,"movement":5,"range":1,"isRanged":false,"recruitmentCost":12,"artKey":"unit.flora.scout"}]}
        """);
        var duplicate = duplicateTile ? ", { \"q\": 0, \"r\": 0, \"terrain\": \"grass\" }" : string.Empty;
        File.WriteAllText(Path.Combine(mapsDirectory, "map.json"), $$"""
        {
          "id":"fixture-map","name":"Fixture","width":2,"height":1,"start":{"q":0,"r":0},
          "tiles":[{"q":0,"r":0,"terrain":"grass"},{"q":1,"r":0,"terrain":"grass"}{{duplicate}}],
          "pointsOfInterest":[{"id":"quest-one","type":"quest","name":"Quest","q":1,"r":0}]
        }
        """);
        File.WriteAllText(Path.Combine(campaignsDirectory, "campaign.json"), $$"""
        {
          "id":"fixture","name":"Fixture","description":"Fixture campaign",
          "playerFactionId":"flora","enemyFactionId":"human","mapFile":"{{mapFile}}",
          "hero":{"id":"hero","name":"Hero","startingGold":100},
          "recruitment":[{"unitId":"{{unitId}}","startingStock":5}],
          "quests":[{"id":"quest","title":"Quest","pointOfInterestId":"{{pointId}}","choices":[{"id":"choice","description":"Choice","goldChange":5,"reputationChange":1}]}]
        }
        """);
        return folder;
    }

    private string WriteSiteContent(string pointId, string siteId, string unitId,
        string? extraSite = null, string? extraPoint = null, string? offers = null)
    {
        var folder = WriteContent("flora.scout", "quest-one", duplicateTile: false);
        var unitsPath = Path.Combine(folder, "content", "units", "units.json");
        File.WriteAllText(unitsPath, """
        {"units":[
          {"id":"flora.scout","factionId":"flora","name":"Scout","hitPoints":8,"damage":3,"speed":7,"movement":5,"range":1,"isRanged":false,"recruitmentCost":12,"artKey":"unit.flora.scout"},
          {"id":"human.guard","factionId":"human","name":"Guard","hitPoints":8,"damage":3,"speed":7,"movement":5,"range":1,"isRanged":false,"recruitmentCost":12,"artKey":"unit.human.guard"}
        ]}
        """);
        var mapPath = Path.Combine(folder, "content", "maps", "map.json");
        var map = File.ReadAllText(mapPath);
        var point = "{\"id\":\"site-point\",\"type\":\"recruitment\",\"name\":\"Site\",\"q\":0,\"r\":0,\"recruitmentSiteId\":\"site-one\"}";
        map = map.Replace("\"pointsOfInterest\":[", $"\"pointsOfInterest\":[{point}," + (extraPoint is null ? "" : extraPoint + ","), StringComparison.Ordinal);
        File.WriteAllText(mapPath, map);
        var campaignPath = Path.Combine(folder, "content", "campaigns", "campaign.json");
        var campaign = File.ReadAllText(campaignPath);
        var offerJson = offers ?? $$"""[{"unitId":"{{unitId}}","startingStock":5}]""";
        var siteJson = $$"""{"id":"{{siteId}}","pointOfInterestId":"{{pointId}}","offers":{{offerJson}}}""";
        campaign = campaign.Replace("\"recruitment\":[", $"\"recruitmentSites\":[{siteJson}" +
            (extraSite is null ? "" : "," + extraSite) + "],\"recruitment\":[", StringComparison.Ordinal);
        File.WriteAllText(campaignPath, campaign);
        return folder;
    }
}
