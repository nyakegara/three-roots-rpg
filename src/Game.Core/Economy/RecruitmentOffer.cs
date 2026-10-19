namespace Game.Core.Economy;

public sealed class RecruitmentOffer
{
    public string UnitTypeId { get; }
    public int BaseUnitCost { get; }

    public RecruitmentOffer(string unitTypeId, int baseUnitCost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitTypeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseUnitCost);
        UnitTypeId = unitTypeId;
        BaseUnitCost = baseUnitCost;
    }
}
