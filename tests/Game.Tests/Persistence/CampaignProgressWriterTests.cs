using Game.Core.Campaigns;
using Game.Core.Characters;
using Game.Core.Geometry;
using Game.Core.Persistence;
using HeroModel = Game.Core.Characters.Hero;
using Xunit;

namespace Game.Tests.Persistence;

public sealed class CampaignProgressWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "three-roots-progress-" + Guid.NewGuid());
    private string SavePath => Path.Combine(_directory, "save.json");
    private string ScoresPath => Path.Combine(_directory, "scores.json");

    [Fact]
    public void Victory_WritesSaveAndOneScoreEvenIfProgressIsSavedAgain()
    {
        var campaign = CreateCampaign();
        var writer = NewWriter();
        Assert.True(writer.Save(campaign, new HexCoord(0, 0)).SaveSucceeded);
        Assert.False(File.Exists(ScoresPath));

        Assert.True(campaign.TryRestoreProgress(new CampaignProgressSnapshot(["final"], false)));
        var first = writer.Save(campaign, new HexCoord(0, 0));
        var second = writer.Save(campaign, new HexCoord(0, 0));

        Assert.True(first.SaveSucceeded);
        Assert.True(first.VictoryRecordSucceeded);
        Assert.True(second.VictoryRecordSucceeded);
        Assert.Single(new LeaderboardStore(ScoresPath).GetTop("flora-01", 10));
        Assert.Equal(1200, Assert.Single(new LeaderboardStore(ScoresPath).GetTop("flora-01", 10)).Score);
        var saved = CampaignSerializer.Load(SavePath).Save!;
        Assert.Equal(["final"], saved.CompletedEncounterIds);
        Assert.False(string.IsNullOrWhiteSpace(saved.RunId));
        Assert.True(new CampaignProgressWriter(SavePath, ScoresPath, "flora-01", "flora-01", saved.RunId)
            .Save(campaign, new HexCoord(0, 0)).VictoryRecordSucceeded);
        Assert.Single(new LeaderboardStore(ScoresPath).GetTop("flora-01", 10));
    }

    [Fact]
    public void DefeatDoesNotCreateScore()
    {
        var campaign = CreateCampaign();
        Assert.True(campaign.TryRestoreProgress(new CampaignProgressSnapshot([], true)));

        var result = NewWriter().Save(campaign, new HexCoord(0, 0));

        Assert.True(result.SaveSucceeded);
        Assert.False(File.Exists(ScoresPath));
    }

    [Fact]
    public void CorruptLeaderboardDoesNotEraseSuccessfulCampaignSave()
    {
        var campaign = CreateCampaign();
        Assert.True(campaign.TryRestoreProgress(new CampaignProgressSnapshot(["final"], false)));
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ScoresPath, "{broken");

        var result = NewWriter().Save(campaign, new HexCoord(0, 0));

        Assert.True(result.SaveSucceeded);
        Assert.False(result.VictoryRecordSucceeded);
        Assert.True(CampaignSerializer.Load(SavePath).Success);
        Assert.Equal("{broken", File.ReadAllText(ScoresPath));

        var runId = CampaignSerializer.Load(SavePath).Save!.RunId;
        File.WriteAllText(ScoresPath, "[]");
        var retry = new CampaignProgressWriter(SavePath, ScoresPath, "flora-01", "flora-01", runId)
            .Save(campaign, new HexCoord(0, 0));
        Assert.True(retry.VictoryRecordSucceeded);
        Assert.Single(new LeaderboardStore(ScoresPath).GetTop("flora-01", 10));
    }

    private CampaignProgressWriter NewWriter() => new(SavePath, ScoresPath, "flora-01", "flora-01");

    private static Campaign CreateCampaign() => new(
        new HeroModel("hero", "Лиара", startingGold: 100), [],
        encounters: [new CampaignEncounter("final", "beach", "Final", 1,
            [new EncounterTroop("human.militia", 1)], 0, 0)],
        progression: new CampaignProgressionRules(victoryEncounterId: "final", defeatOnArmyWipe: true));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
