using System.Collections.ObjectModel;

namespace Game.Core.Combat;

/// <summary>A permanent snapshot of the player's original stacks that survived a completed battle.</summary>
public sealed class BattleOutcome
{
    private readonly ReadOnlyCollection<BattleStackSurvivor> _survivors;

    public Guid BattleId { get; }
    public IReadOnlyList<BattleStackSurvivor> SurvivingPlayerStacks => _survivors;

    internal BattleOutcome(Guid battleId, IEnumerable<BattleStackSurvivor> survivors)
    {
        ArgumentNullException.ThrowIfNull(survivors);
        BattleId = battleId;
        _survivors = Array.AsReadOnly(survivors
            .Select(stack => new BattleStackSurvivor(stack.UnitTypeId, stack.Count))
            .ToArray());
    }
}

public sealed record BattleStackSurvivor
{
    public string UnitTypeId { get; }
    public int Count { get; }

    internal BattleStackSurvivor(string unitTypeId, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitTypeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        UnitTypeId = unitTypeId;
        Count = count;
    }
}
