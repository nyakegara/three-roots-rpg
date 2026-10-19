using System.Text.Json;

namespace Game.Core.Persistence;

/// <summary>Persists the ten best results for each campaign independently of campaign saves.</summary>
public sealed class LeaderboardStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public LeaderboardStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public void Add(ScoreEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var entries = ReadEntries();
        entries.Add(entry);
        var bestEntries = entries.GroupBy(result => result.CampaignId, StringComparer.Ordinal)
            .SelectMany(campaign => campaign.OrderByDescending(result => result.Score).Take(10))
            .ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(bestEntries, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public IReadOnlyList<ScoreEntry> GetTop(string campaignId, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return Array.AsReadOnly(ReadEntries()
            .Where(entry => string.Equals(entry.CampaignId, campaignId, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.Score)
            .Take(Math.Min(count, 10))
            .ToArray());
    }

    private List<ScoreEntry> ReadEntries()
    {
        if (!File.Exists(_path)) return new List<ScoreEntry>();
        try
        {
            var entries = JsonSerializer.Deserialize<List<ScoreEntry>>(File.ReadAllText(_path));
            if (entries is null || entries.Any(entry => entry is null))
                throw new InvalidDataException("The leaderboard must contain a list of valid scores.");
            return entries;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("The leaderboard contains invalid score data.", exception);
        }
    }
}
