namespace Game.Core.Economy;

public sealed class RecruitmentOffer
{
    public string UnitTypeId { get; }
    public int BaseUnitCost { get; }
    public int StartingStock { get; }
    public int RemainingStock { get; private set; }

    public RecruitmentOffer(string unitTypeId, int baseUnitCost, int startingStock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitTypeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseUnitCost);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startingStock);
        UnitTypeId = unitTypeId;
        BaseUnitCost = baseUnitCost;
        StartingStock = startingStock;
        RemainingStock = startingStock;
    }

    internal void ConsumeStock(int count)
    {
        if (count <= 0 || count > RemainingStock)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        RemainingStock -= count;
    }

    internal void RestoreStock(int remainingStock)
    {
        if (remainingStock < 0 || remainingStock > StartingStock)
            throw new ArgumentOutOfRangeException(nameof(remainingStock));
        RemainingStock = remainingStock;
    }
}
