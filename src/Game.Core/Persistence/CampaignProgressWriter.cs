using Game.Core.Campaigns;
using Game.Core.Geometry;

namespace Game.Core.Persistence;

/// <summary>Saves campaign progress and records a victory once per playthrough.</summary>
public sealed class CampaignProgressWriter
{
    private readonly string _savePath;
    private readonly LeaderboardStore _leaderboard;
    private readonly string _campaignId;
    private readonly string _mapId;
    private readonly string _runId;
    private bool _victoryRecorded;

    public CampaignProgressWriter(string savePath, string leaderboardPath, string campaignId, string mapId,
        string? runId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(savePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        _savePath = savePath;
        _leaderboard = new LeaderboardStore(leaderboardPath);
        _campaignId = campaignId;
        _mapId = mapId;
        _runId = string.IsNullOrWhiteSpace(runId) ? Guid.NewGuid().ToString("N") : runId;
    }

    public CampaignProgressWriteResult Save(Campaign campaign, HexCoord position,
        Game.Core.Navigation.MapPosition? adventurePosition = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var save = campaign.CreateSave(_campaignId, _mapId, position) with
            { RunId = _runId, AdventurePosition = adventurePosition };
        var result = CampaignSerializer.Save(_savePath, save);
        if (!result.Success)
            return new CampaignProgressWriteResult(false, false, result.Message);

        if (!campaign.IsVictory || _victoryRecorded)
            return new CampaignProgressWriteResult(true, _victoryRecorded, result.Message);

        // A result is written only after the victory save succeeds. If the leaderboard is
        // damaged, the save survives and the next checkpoint can retry recording the score.
        try
        {
            _leaderboard.Add(new ScoreEntry(_campaignId, campaign.Hero.Name, CalculateScore(save), _runId));
            _victoryRecorded = true;
            return new CampaignProgressWriteResult(true, true, "Victory saved and recorded.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidDataException or ArgumentException or NotSupportedException)
        {
            return new CampaignProgressWriteResult(true, false,
                "Campaign saved, but the victory record could not be written: " + exception.Message);
        }
    }

    private static int CalculateScore(CampaignSave save)
    {
        // Gold rewards careful spending; optional encounters and surviving troops add points.
        var score = 1000L + save.CompletedEncounterIds.Count * 100L +
                    save.Army.Sum(stack => (long)stack.Count) * 10L + save.Gold;
        return (int)Math.Min(score, int.MaxValue);
    }
}

public sealed record CampaignProgressWriteResult(bool SaveSucceeded, bool VictoryRecordSucceeded, string Message);
