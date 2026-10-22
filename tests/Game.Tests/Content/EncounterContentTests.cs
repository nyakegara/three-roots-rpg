using System.Text.Json.Nodes;
using Game.Core.Content;
using Xunit;

namespace Game.Tests.Content;

public sealed class EncounterContentTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"encounters-{Guid.NewGuid():N}");

    [Fact]
    public void Load_RepeatedTypesRemainFourSeparateStacks()
    {
        var encounter = JsonNode.Parse(ValidEncounter)!.AsObject();
        encounter["enemyArmy"] = JsonNode.Parse("[" + string.Join(",", Enumerable.Repeat(
            "{\"unitId\":\"human.militia\",\"count\":5}", 4)) + "]");
        var result = Load("[" + encounter.ToJsonString() + "]");
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        var army = Assert.Single(result.Definition!.Encounters).EnemyArmy;
        Assert.Equal(4, army.Count);
        Assert.All(army, troop => Assert.Equal(5, troop.Count));
    }

    [Fact]
    public void Load_EncounterAtExistingQuestPoint_Succeeds()
    {
        var result = Load("[" + ValidEncounter + "]");

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        var encounter = Assert.Single(result.Definition!.Encounters);
        Assert.Equal("road-patrol", encounter.Id);
        Assert.Equal("pasture-request", encounter.PointOfInterestId);
        Assert.Equal("Дозор у стройки", encounter.Name);
        Assert.Equal(20261019, encounter.Seed);
        Assert.Equal(60, encounter.GoldReward);
        Assert.Equal(60, encounter.ExperienceReward);
        var troop = Assert.Single(encounter.EnemyArmy);
        Assert.Equal("human.militia", troop.UnitTypeId);
        Assert.Equal(4, troop.Count);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData(null)]
    public void Load_OptionalEncounters_Succeeds(string? encounters)
    {
        var result = Load(encounters);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(result.Definition!.Encounters);
    }

    [Theory]
    [InlineData("id", "\"\"", "id")]
    [InlineData("name", "null", "name")]
    [InlineData("seed", "null", "seed")]
    [InlineData("pointOfInterestId", "\"missing-point\"", "missing point")]
    [InlineData("pointOfInterestId", "\"starting-clearing\"", "recruitment")]
    [InlineData("goldReward", "-1", "goldReward")]
    [InlineData("goldReward", "null", "goldReward")]
    [InlineData("experienceReward", "-1", "experienceReward")]
    [InlineData("experienceReward", "null", "experienceReward")]
    [InlineData("enemyArmy", "[]", "enemyArmy")]
    [InlineData("enemyArmy", "[null]", "unitId")]
    [InlineData("enemyArmy", "[{\"unitId\":\"absent\",\"count\":1}]", "unknown unitId")]
    [InlineData("enemyArmy", "[{\"unitId\":\"flora.sprout\",\"count\":1}]", "enemy faction")]
    [InlineData("enemyArmy", "[{\"unitId\":\"human.militia\",\"count\":0}]", "positive count")]
    [InlineData("enemyArmy", "[{\"unitId\":\"human.militia\",\"count\":-1}]", "positive count")]
    [InlineData("enemyArmy", "[{\"unitId\":\"human.militia\"}]", "positive count")]
    [InlineData("enemyArmy", "[{\"unitId\":\"human.militia\",\"count\":2147483647}]", "hit points")]
    public void Load_InvalidEncounter_ReturnsDiagnostic(string property, string value, string expected)
    {
        var encounter = JsonNode.Parse(ValidEncounter)!.AsObject();
        encounter[property] = JsonNode.Parse(value);

        var result = Load("[" + encounter.ToJsonString() + "]");

        Assert.False(result.Success);
        Assert.Null(result.Definition);
        Assert.Contains(result.Errors, error => error.Message.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("{}")] 
    [InlineData("[{\"unknown\":true}]")]
    [InlineData("[{\"seed\":\"tomorrow\"}]")]
    [InlineData("[{\"goldReward\":2147483648}]")]
    public void Load_MalformedEncounters_ReturnsContentErrors(string encounters)
    {
        var result = Load(encounters);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData(false, "duplicate encounter id")]
    [InlineData(true, "multiple encounters")]
    public void Load_DuplicateEncounterLink_ReturnsDiagnostic(bool distinctId, string expected)
    {
        var second = JsonNode.Parse(ValidEncounter)!.AsObject();
        if (distinctId) second["id"] = "another";

        var result = Load("[" + ValidEncounter + "," + second.ToJsonString() + "]");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_MoreThanFiveEnemyStacks_ReturnsDiagnostic()
    {
        var encounter = JsonNode.Parse(ValidEncounter)!.AsObject();
        encounter["enemyArmy"] = JsonNode.Parse("[" + string.Join(",", Enumerable.Repeat(
            "{\"unitId\":\"human.militia\",\"count\":1}", 6)) + "]");

        var result = Load("[" + encounter.ToJsonString() + "]");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("five", StringComparison.OrdinalIgnoreCase));
    }

    private const string ValidEncounter = """
        {"id":"road-patrol","pointOfInterestId":"pasture-request","name":"Дозор у стройки","seed":20261019,
         "enemyArmy":[{"unitId":"human.militia","count":4}],"goldReward":60,"experienceReward":60}
        """;

    private CampaignLoadResult Load(string? encounters)
    {
        var content = Path.Combine(AppContext.BaseDirectory, "content");
        Directory.CreateDirectory(Path.Combine(_folder, "campaigns"));
        Directory.CreateDirectory(Path.Combine(_folder, "maps"));
        File.Copy(Path.Combine(content, "maps", "flora-01.json"), Path.Combine(_folder, "maps", "flora-01.json"), true);
        var campaign = JsonNode.Parse(File.ReadAllText(Path.Combine(content, "campaigns", "flora-01.json")))!.AsObject();
        campaign.Remove("progression");
        if (encounters is null) campaign.Remove("encounters");
        else campaign["encounters"] = JsonNode.Parse(encounters);
        var path = Path.Combine(_folder, "campaigns", "fixture.json");
        File.WriteAllText(path, campaign.ToJsonString());
        return new CampaignLoader(UnitCatalog.Load(Path.Combine(content, "units", "units.json")).Catalog!).Load(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
