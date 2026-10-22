using Game.Core.Characters;

namespace Game.Core.Economy;

/// <summary>Applies recruitment prices, gold costs, and army capacity rules.</summary>
public sealed class EconomyRules
{
    private const int ReputationPriceChangePercent = 10;

    public int? GetAdjustedUnitCost(Hero hero, RecruitmentSite site, RecruitmentOffer offer)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(offer);
        if (hero.FactionId is null || site.FactionId is null ||
            !hero.FactionId.Equals(site.FactionId, StringComparison.Ordinal))
            return offer.BaseUnitCost;

        var pricePercent = 100 - hero.Reputation * ReputationPriceChangePercent;
        var calculatedUnitCost = Math.Max(1L, ((long)offer.BaseUnitCost * pricePercent + 99) / 100);
        return calculatedUnitCost > int.MaxValue ? null : (int)calculatedUnitCost;
    }

    public RecruitmentResult TryRecruit(Hero hero, RecruitmentSite site, string unitTypeId, int count)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(site);
        if (string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return new RecruitmentResult(false, "A unit type and a positive count are required.");
        }

        var offer = site.GetOffer(unitTypeId);
        if (offer is null)
        {
            return new RecruitmentResult(false, $"Unit type '{unitTypeId}' is not available at '{site.Id}'.");
        }

        var unitCostValue = GetAdjustedUnitCost(hero, site, offer);
        if (unitCostValue is null)
        {
            return new RecruitmentResult(false, "The adjusted unit price is too large.");
        }

        var unitCost = unitCostValue.Value;
        var totalCost = (long)unitCost * count;
        if (totalCost > int.MaxValue || !hero.Army.CanAdd(unitTypeId, count))
        {
            return new RecruitmentResult(false, "The army cannot accept this recruitment.", unitCost);
        }

        if (offer.RemainingStock < count)
        {
            return new RecruitmentResult(false, "The recruitment site does not have enough stock.", unitCost, (int)totalCost);
        }

        if (hero.Gold < totalCost)
        {
            return new RecruitmentResult(false, "The hero does not have enough gold.", unitCost, (int)totalCost);
        }

        if (!hero.TryChangeGold(-(int)totalCost))
        {
            return new RecruitmentResult(false, "Recruitment could not be completed.", unitCost, (int)totalCost);
        }

        if (!hero.Army.TryAdd(unitTypeId, count))
        {
            hero.TryChangeGold((int)totalCost);
            return new RecruitmentResult(false, "Recruitment could not be completed.", unitCost, (int)totalCost);
        }

        offer.ConsumeStock(count);
        site.RecordPurchase();
        return new RecruitmentResult(true, "Recruitment completed.", unitCost, (int)totalCost);
    }
}
