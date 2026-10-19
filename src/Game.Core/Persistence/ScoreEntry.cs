namespace Game.Core.Persistence;

/// <summary>A completed campaign result stored in the local leaderboard.</summary>
public sealed class ScoreEntry
{
    public string CampaignId { get; }
    public string PlayerName { get; }
    public int Score { get; }

    public ScoreEntry(string campaignId, string playerName, int score)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
        CampaignId = campaignId;
        PlayerName = playerName;
        Score = score;
    }
}
