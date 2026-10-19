using Game.Core.Characters;

namespace Game.Core.Economy;

/// <summary>Applies recruitment prices, gold costs, and army capacity rules.</summary>
public sealed class EconomyRules
{
    private const int ReputationPriceChangePercent = 10;

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

        var pricePercent = 100 - hero.Reputation * ReputationPriceChangePercent;
        var calculatedUnitCost = Math.Max(1L, ((long)offer.BaseUnitCost * pricePercent + 99) / 100);
        if (calculatedUnitCost > int.MaxValue)
        {
            return new RecruitmentResult(false, "The adjusted unit price is too large.");
        }

        var unitCost = (int)calculatedUnitCost;
        var totalCost = (long)unitCost * count;
        if (totalCost > int.MaxValue || !hero.Army.CanAdd(unitTypeId, count))
        {
            return new RecruitmentResult(false, "The army cannot accept this recruitment.", unitCost);
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

        return new RecruitmentResult(true, "Recruitment completed.", unitCost, (int)totalCost);
    }
}
