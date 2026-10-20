using Game.Core.Geometry;
using Game.Core.Persistence;
using System.Text.Json.Nodes;
using Xunit;

namespace Game.Tests.Persistence;

public sealed class CampaignSerializerTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsCampaignProgress()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "campaign.json");
        var save = new CampaignSave("flora-01", "flora.hero", "Liora", "flora-01", new HexCoord(3, -2),
            240, 2, 175, 2, 1,
            [new SavedTroopStack("flora.scout", 4), new SavedTroopStack("flora.guardian", 2)],
            ["flora.quest.01"], [new SavedQuestChoice("flora.quest.01", "aid-village")],
            ["pathfinder"], ["seed-charm"])
        {
            RecruitmentStocks = [new SavedRecruitmentStock("root-greenhouse", "flora.guard", 0),
                new SavedRecruitmentStock("sunny-clearing", "flora.flame", 7)],
            CompletedEncounterIds = ["peasants-refused", "north-knights"]
        };

        var write = CampaignSerializer.Save(path, save);
        var read = CampaignSerializer.Load(path);

        Assert.True(write.Success, write.Message);
        Assert.True(read.Success, read.Message);
        Assert.Equal(save.CampaignId, read.Save!.CampaignId);
        Assert.Equal(save.HeroPosition, read.Save.HeroPosition);
        Assert.Equal(save.Gold, read.Save.Gold);
        Assert.Equal(save.Reputation, read.Save.Reputation);
        Assert.Equal(save.Army, read.Save.Army);
        Assert.Equal(save.CompletedQuestIds, read.Save.CompletedQuestIds);
        Assert.Equal(save.SelectedChoices, read.Save.SelectedChoices);
        Assert.Equal(save.SkillIds, read.Save.SkillIds);
        Assert.Equal(save.ArtifactIds, read.Save.ArtifactIds);
        Assert.Equal(save.RecruitmentStocks, read.Save.RecruitmentStocks);
        Assert.Equal(save.CompletedEncounterIds, read.Save.CompletedEncounterIds);
        Assert.False(read.Save.IsDefeated);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void SaveAndLoad_PreservesDefeatAndRejectsArmyInDefeatedState()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "defeated.json");
        var defeated = SampleSave() with { IsDefeated = true, CompletedEncounterIds = ["first-battle"] };
        Assert.True(CampaignSerializer.Save(path, defeated).Success);
        var loaded = CampaignSerializer.Load(path);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.Save!.IsDefeated);
        Assert.Equal(["first-battle"], loaded.Save.CompletedEncounterIds);
        Assert.False(CampaignSerializer.Save(path, defeated with
        {
            Army = [new SavedTroopStack("flora.thorn-guard", 1)]
        }).Success);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("negative")]
    public void Load_RejectsInvalidRecruitmentStockRecords(string invalidKind)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "invalid-stock.json");
        var json = JsonNode.Parse(SaveJson(1))!;
        json["recruitmentStocks"] = invalidKind == "duplicate"
            ? new JsonArray(JsonNode.Parse("""{"siteId":"site","unitId":"unit","remainingStock":1}"""),
                JsonNode.Parse("""{"siteId":"site","unitId":"unit","remainingStock":2}"""))
            : new JsonArray(JsonNode.Parse("""{"siteId":"site","unitId":"unit","remainingStock":-1}"""));
        File.WriteAllText(path, json.ToJsonString());

        var result = CampaignSerializer.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Save);
        Assert.Contains("stock", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_InvalidRecruitmentStocks_DoesNotReplaceExistingFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "campaign.json");
        Assert.True(CampaignSerializer.Save(path, SampleSave()).Success);
        var original = File.ReadAllText(path);
        var invalid = SampleSave() with
        {
            RecruitmentStocks = [new SavedRecruitmentStock("site", "unit", -1)]
        };

        var result = CampaignSerializer.Save(path, invalid);

        Assert.False(result.Success);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void Load_ReportsMissingFileWithoutThrowing()
    {
        using var directory = new TemporaryDirectory();

        var result = CampaignSerializer.Load(Path.Combine(directory.Path, "missing.json"));

        Assert.False(result.Success);
        Assert.Null(result.Save);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_ReportsMalformedJsonWithoutThrowing()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "broken.json");
        File.WriteAllText(path, "{ this is not json }");

        var result = CampaignSerializer.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Save);
        Assert.Contains("JSON", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsUnknownSchemaVersion()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "future.json");
        File.WriteAllText(path, SaveJson(42));

        var result = CampaignSerializer.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Save);
        Assert.Contains("version", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsSaveWithoutSchemaVersion()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "unversioned.json");
        var json = JsonNode.Parse(SaveJson(1))!;
        json.AsObject().Remove("schemaVersion");
        File.WriteAllText(path, json.ToJsonString());

        var result = CampaignSerializer.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Save);
        Assert.Contains("schemaVersion", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsSaveWithoutHeroPosition()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "no-position.json");
        var json = JsonNode.Parse(SaveJson(1))!;
        json.AsObject().Remove("heroPosition");
        File.WriteAllText(path, json.ToJsonString());

        var result = CampaignSerializer.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Save);
        Assert.Contains("heroPosition", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsCompletedQuestWithoutSelectedChoice()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "quest-without-choice.json");
        var json = JsonNode.Parse(SaveJson(1))!;
        json["completedQuestIds"] = new JsonArray(JsonValue.Create("flora.quest.01"));
        File.WriteAllText(path, json.ToJsonString());

        var result = CampaignSerializer.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Save);
        Assert.Contains("choices", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("campaignId")]
    [InlineData("heroId")]
    [InlineData("heroName")]
    [InlineData("mapId")]
    [InlineData("gold")]
    [InlineData("reputation")]
    [InlineData("experience")]
    [InlineData("level")]
    [InlineData("skillPoints")]
    [InlineData("army")]
    [InlineData("completedQuestIds")]
    [InlineData("selectedChoices")]
    [InlineData("skillIds")]
    [InlineData("artifactIds")]
    [InlineData("recruitmentStocks")]
    [InlineData("completedEncounterIds")]
    [InlineData("isDefeated")]
    public void Load_RejectsSaveMissingExplicitField(string field)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "incomplete.json");
        var json = JsonNode.Parse(SaveJson(1))!;
        json.AsObject().Remove(field);
        File.WriteAllText(path, json.ToJsonString());

        var result = CampaignSerializer.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Save);
        Assert.Contains(field, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_WhenTargetDirectoryDoesNotExist_ReturnsIoError()
    {
        using var directory = new TemporaryDirectory();
        var blocker = Path.Combine(directory.Path, "blocker");
        File.WriteAllText(blocker, "a file cannot be a parent directory");
        var path = Path.Combine(blocker, "campaign.json");

        var result = CampaignSerializer.Save(path, SampleSave());

        Assert.False(result.Success);
        Assert.Contains("save", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(blocker));
    }

    [Fact]
    public void Save_ReplacesPreviousSaveWithoutLeavingTemporaryFiles()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "campaign.json");
        var first = SampleSave();
        var second = first with { Gold = 725 };

        Assert.True(CampaignSerializer.Save(path, first).Success);
        var replacement = CampaignSerializer.Save(path, second);
        var read = CampaignSerializer.Load(path);

        Assert.True(replacement.Success, replacement.Message);
        Assert.True(read.Success, read.Message);
        Assert.Equal(725, read.Save!.Gold);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    private static CampaignSave SampleSave() => new("flora-01", "flora.hero", "Liora", "flora-01",
        new HexCoord(0, 0), 10, 0, 0, 1, 0, [], [], [], [], [])
    {
        RecruitmentStocks = []
    };

    private static string SaveJson(int schemaVersion) => $$"""
        { "schemaVersion": {{schemaVersion}}, "campaignId": "flora-01", "heroId": "flora.hero", "heroName": "Liora",
          "mapId": "flora-01", "heroPosition": { "q": 0, "r": 0 }, "gold": 0,
          "reputation": 0, "experience": 0, "level": 1, "skillPoints": 0,
          "army": [], "completedQuestIds": [], "selectedChoices": [], "skillIds": [], "artifactIds": [], "recruitmentStocks": [],
          "completedEncounterIds": [], "isDefeated": false }
        """;

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
