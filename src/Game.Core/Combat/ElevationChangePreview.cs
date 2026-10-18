namespace Game.Core.Combat;

/// <summary>Describes height and high-ground range changes against individual target heights.</summary>
/// <param name="LevelDelta">Destination height minus the current height.</param>
/// <param name="GainedRangeBonusTargetCount">Targets that become lower than the attacker.</param>
/// <param name="LostRangeBonusTargetCount">Targets that are no longer lower than the attacker.</param>
public readonly record struct ElevationChangePreview(
    int LevelDelta,
    int GainedRangeBonusTargetCount,
    int LostRangeBonusTargetCount,
    int TargetCount)
{
    public const int HighGroundRangeBonus = 2;

    public static ElevationChangePreview Between(
        int currentHeight,
        int destinationHeight,
        IEnumerable<int> targetHeights)
    {
        ArgumentNullException.ThrowIfNull(targetHeights);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationHeight);

        var gained = 0;
        var lost = 0;
        var count = 0;
        foreach (var targetHeight in targetHeights)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(targetHeight);
            count++;
            if (currentHeight <= targetHeight && destinationHeight > targetHeight)
                gained++;
            else if (currentHeight > targetHeight && destinationHeight <= targetHeight)
                lost++;
        }

        return new ElevationChangePreview(destinationHeight - currentHeight, gained, lost, count);
    }
}
