namespace Game.Core.Characters;

public sealed class TroopStack
{
    public string UnitTypeId { get; }
    public int Count { get; private set; }

    internal TroopStack(string unitTypeId, int count)
    {
        UnitTypeId = unitTypeId;
        Count = count;
    }

    internal bool TryAdd(int count)
    {
        if (count <= 0 || (long)Count + count > int.MaxValue)
        {
            return false;
        }

        Count += count;
        return true;
    }
}
