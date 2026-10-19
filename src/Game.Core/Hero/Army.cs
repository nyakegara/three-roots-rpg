using System.Collections.ObjectModel;

namespace Game.Core.Characters;

/// <summary>A hero may command up to five distinct troop stacks.</summary>
public sealed class Army
{
    public const int MaximumStacks = 5;

    private readonly List<TroopStack> _stacks = [];
    private readonly ReadOnlyCollection<TroopStack> _stackView;

    public IReadOnlyList<TroopStack> Stacks => _stackView;

    public Army()
    {
        _stackView = _stacks.AsReadOnly();
    }

    internal bool CanAdd(string unitTypeId, int count)
    {
        if (count <= 0)
        {
            return false;
        }

        var existing = FindStack(unitTypeId);
        return existing is null ? _stacks.Count < MaximumStacks : (long)existing.Count + count <= int.MaxValue;
    }

    internal bool TryAdd(string unitTypeId, int count)
    {
        if (!CanAdd(unitTypeId, count))
        {
            return false;
        }

        var existing = FindStack(unitTypeId);
        if (existing is not null)
        {
            return existing.TryAdd(count);
        }

        _stacks.Add(new TroopStack(unitTypeId, count));
        return true;
    }

    private TroopStack? FindStack(string unitTypeId) =>
        _stacks.FirstOrDefault(stack => string.Equals(stack.UnitTypeId, unitTypeId, StringComparison.Ordinal));
}
