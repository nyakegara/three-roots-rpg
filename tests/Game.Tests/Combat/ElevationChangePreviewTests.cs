using Game.Core.Combat;
using Xunit;

namespace Game.Tests.Combat;

public sealed class ElevationChangePreviewTests
{
    [Fact]
    public void Between_HigherDestination_GainsBonusOnlyAgainstTargetsNowBelow()
    {
        var change = ElevationChangePreview.Between(1, 2, [0, 1, 2]);

        Assert.Equal(1, change.LevelDelta);
        Assert.Equal(1, change.GainedRangeBonusTargetCount);
        Assert.Equal(0, change.LostRangeBonusTargetCount);
    }

    [Fact]
    public void Between_LowerDestination_LosesBonusOnlyAgainstTargetsNoLongerBelow()
    {
        var change = ElevationChangePreview.Between(2, 1, [0, 1, 2]);

        Assert.Equal(-1, change.LevelDelta);
        Assert.Equal(0, change.GainedRangeBonusTargetCount);
        Assert.Equal(1, change.LostRangeBonusTargetCount);
    }

    [Fact]
    public void Between_TwoLevelDrop_LosesBonusForTargetsBetweenTheHeights()
    {
        var change = ElevationChangePreview.Between(2, 0, [0, 1, 2]);

        Assert.Equal(-2, change.LevelDelta);
        Assert.Equal(0, change.GainedRangeBonusTargetCount);
        Assert.Equal(2, change.LostRangeBonusTargetCount);
    }

    [Fact]
    public void Between_LowerTarget_RemainsBelowAfterOneLevelDescent()
    {
        var change = ElevationChangePreview.Between(2, 1, [0]);

        Assert.Equal(-1, change.LevelDelta);
        Assert.Equal(0, change.GainedRangeBonusTargetCount);
        Assert.Equal(0, change.LostRangeBonusTargetCount);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void Between_NegativeHeight_ThrowsArgumentOutOfRangeException(
        int currentHeight, int destinationHeight, int targetHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ElevationChangePreview.Between(currentHeight, destinationHeight, [targetHeight]));
    }

    [Fact]
    public void Between_NullTargetHeights_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ElevationChangePreview.Between(0, 1, null!));
    }

    [Fact]
    public void Between_NoTargets_OnlyReportsTheLevelDifference()
    {
        var change = ElevationChangePreview.Between(2, 0, Array.Empty<int>());

        Assert.Equal(-2, change.LevelDelta);
        Assert.Equal(0, change.GainedRangeBonusTargetCount);
        Assert.Equal(0, change.LostRangeBonusTargetCount);
        Assert.Equal(0, change.TargetCount);
    }
}
