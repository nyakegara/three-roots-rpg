using Game.Core.Persistence;
using Xunit;

namespace Game.Tests.Persistence;

public sealed class LeaderboardStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "three-roots-leaderboard-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_directory, "scores.json");

    [Fact]
    public void Add_PersistsEntryForAnotherStoreInstance()
    {
        new LeaderboardStore(FilePath).Add(new ScoreEntry("flora", "Keeper", 125));
        var entry = Assert.Single(new LeaderboardStore(FilePath).GetTop("flora", 10));
        Assert.Equal("flora", entry.CampaignId);
        Assert.Equal("Keeper", entry.PlayerName);
        Assert.Equal(125, entry.Score);
    }

    [Fact]
    public void GetTop_OrdersScoresDescendingAndHonorsCount()
    {
        var store = new LeaderboardStore(FilePath);
        store.Add(new ScoreEntry("flora", "First", 20));
        store.Add(new ScoreEntry("flora", "Second", 90));
        store.Add(new ScoreEntry("flora", "Third", 50));
        Assert.Equal(new[] { 90, 50 }, store.GetTop("flora", 2).Select(entry => entry.Score));
    }

    [Fact]
    public void Add_KeepsOnlyBestTenEntriesPerCampaign()
    {
        var store = new LeaderboardStore(FilePath);
        foreach (var score in new[] { 3, 12, 1, 9, 11, 5, 8, 6, 4, 10, 2, 7 })
            store.Add(new ScoreEntry("flora", "Keeper", score));
        store.Add(new ScoreEntry("flora", "Low score", 0));
        Assert.Equal(new[] { 12, 11, 10, 9, 8, 7, 6, 5, 4, 3 },
            new LeaderboardStore(FilePath).GetTop("flora", 100).Select(entry => entry.Score));
    }

    [Fact]
    public void Add_AnotherCampaignPreservesEarlierCampaignResults()
    {
        new LeaderboardStore(FilePath).Add(new ScoreEntry("flora", "Keeper", 125));
        new LeaderboardStore(FilePath).Add(new ScoreEntry("humans", "Scout", 250));
        var store = new LeaderboardStore(FilePath);
        Assert.Equal(125, Assert.Single(store.GetTop("flora", 10)).Score);
        Assert.Equal(250, Assert.Single(store.GetTop("humans", 10)).Score);
        Assert.Empty(store.GetTop("missing", 10));
    }

    [Fact]
    public void GetTop_MissingFileReturnsEmptyWithoutCreatingFile()
    {
        Assert.Empty(new LeaderboardStore(FilePath).GetTop("flora", 10));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void GetTop_ZeroCountReturnsEmpty()
    {
        var store = new LeaderboardStore(FilePath);
        store.Add(new ScoreEntry("flora", "Keeper", 125));
        Assert.Empty(store.GetTop("flora", 0));
    }

    [Fact]
    public void GetTop_NegativeCountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LeaderboardStore(FilePath).GetTop("flora", -1));
    }

    [Theory]
    [InlineData("{ broken json")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    public void Add_InvalidFileFailsWithoutReplacingExistingContents(string contents)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, contents);
        Assert.Throws<InvalidDataException>(() =>
            new LeaderboardStore(FilePath).Add(new ScoreEntry("flora", "Keeper", 125)));
        Assert.Equal(contents, File.ReadAllText(FilePath));
    }

    [Fact]
    public void Add_SeparateStoreInstancesPreserveEachOthersEntries()
    {
        var first = new LeaderboardStore(FilePath);
        var second = new LeaderboardStore(FilePath);
        first.Add(new ScoreEntry("flora", "Keeper", 125));
        second.Add(new ScoreEntry("flora", "Scout", 250));
        Assert.Equal(new[] { 250, 125 }, first.GetTop("flora", 10).Select(entry => entry.Score));
    }

    [Fact]
    public void InvalidArgumentsAreRejectedBeforeCreatingAFile()
    {
        var store = new LeaderboardStore(FilePath);
        Assert.Throws<ArgumentException>(() => new LeaderboardStore(" "));
        Assert.Throws<ArgumentException>(() => new ScoreEntry(" ", "Keeper", 10));
        Assert.Throws<ArgumentException>(() => new ScoreEntry("flora", " ", 10));
        Assert.Throws<ArgumentNullException>(() => store.Add(null!));
        Assert.Throws<ArgumentException>(() => store.GetTop(" ", 10));
        Assert.False(File.Exists(FilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
