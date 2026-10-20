using Game.Core.Economy;
using Game.Core.Characters;
using HeroModel = Game.Core.Characters.Hero;
using Xunit;

namespace Game.Tests.Economy;

public sealed class EconomyRulesTests
{
    [Theory]
    [InlineData(2, 80)]
    [InlineData(-2, 120)]
    public void GetAdjustedUnitCost_UsesReputationPriceForDisplay(int reputation, int expected)
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 500, initialReputation: reputation,
            factionId: "flora");
        var offer = new RecruitmentOffer("flora.guard", 100, 5);
        var site = new RecruitmentSite("grove", [offer], "flora");

        var price = new EconomyRules().GetAdjustedUnitCost(hero, site, offer);

        Assert.Equal(expected, price);
    }

    [Fact]
    public void GetAdjustedUnitCost_WithoutHeroFactionDoesNotApplyCampaignReputation()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 500, initialReputation: 2);
        var offer = new RecruitmentOffer("human.militia", 100, 5);
        var site = new RecruitmentSite("human-pasture", [offer], "human");

        var price = new EconomyRules().GetAdjustedUnitCost(hero, site, offer);

        Assert.Equal(100, price);
    }

    private readonly EconomyRules _rules = new();

    [Fact]
    public void TryRecruit_Success_ChargesAdjustedCostAndConsumesExactStock()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 500, initialReputation: 2, factionId: "flora");
        var offer = new RecruitmentOffer("flora.scout", 100, 7);
        var site = new RecruitmentSite("village", [offer], "flora");

        var result = _rules.TryRecruit(hero, site, "flora.scout", 3);

        Assert.True(result.Success);
        Assert.Equal(80, result.UnitCost);
        Assert.Equal(240, result.TotalCost);
        Assert.Equal(260, hero.Gold);
        Assert.Single(hero.Army.Stacks);
        Assert.Equal(3, hero.Army.Stacks.Single().Count);
        Assert.Equal(4, offer.RemainingStock);
    }

    [Fact]
    public void TryRecruit_InsufficientStock_LeavesGoldArmyAndStockUnchanged()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 500);
        var offer = new RecruitmentOffer("flora.scout", 10, 2);
        var site = new RecruitmentSite("village", [offer]);

        var result = _rules.TryRecruit(hero, site, "flora.scout", 3);

        Assert.False(result.Success);
        Assert.Equal(500, hero.Gold);
        Assert.Empty(hero.Army.Stacks);
        Assert.Equal(2, offer.RemainingStock);
    }

    [Fact]
    public void TryRecruit_FullArmy_LeavesGoldArmyAndStockUnchanged()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 500);
        var offers = Enumerable.Range(1, 6)
            .Select(index => new RecruitmentOffer($"unit-{index}", 10, 4)).ToArray();
        var site = new RecruitmentSite("village", offers);
        for (var index = 1; index <= 5; index++)
        {
            Assert.True(_rules.TryRecruit(hero, site, $"unit-{index}", 1).Success);
        }

        var result = _rules.TryRecruit(hero, site, "unit-6", 1);

        Assert.False(result.Success);
        Assert.Equal(450, hero.Gold);
        Assert.Equal(5, hero.Army.Stacks.Count);
        Assert.All(hero.Army.Stacks, stack => Assert.Equal(1, stack.Count));
        Assert.Equal(4, offers[5].RemainingStock);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void RecruitmentOffer_NonpositivePriceOrStock_IsRejected(int price, int stock)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecruitmentOffer("flora.scout", price, stock));
    }

    [Theory]
    [InlineData(2, 80)]
    [InlineData(-2, 120)]
    public void TryRecruit_ReputationChangesPriceWithoutBlockingAccess(int reputation, int expectedUnitPrice)
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 500, initialReputation: reputation, factionId: "flora");
        var site = Site(("flora.scout", 100));

        var result = _rules.TryRecruit(hero, site, "flora.scout", 2);

        Assert.True(result.Success);
        Assert.Equal(expectedUnitPrice * 2, result.TotalCost);
        Assert.Equal(500 - expectedUnitPrice * 2, hero.Gold);
        Assert.Equal(2, hero.Army.Stacks.Single().Count);
    }

    [Fact]
    public void TryRecruit_FloraReputationDoesNotChangeHumanSettlementPrices()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 500, initialReputation: 2, factionId: "flora");
        var site = new RecruitmentSite("human-pasture",
            [new RecruitmentOffer("human.militia", 100, 5)], "human");

        var result = _rules.TryRecruit(hero, site, "human.militia", 1);

        Assert.True(result.Success);
        Assert.Equal(100, result.UnitCost);
        Assert.Equal(400, hero.Gold);
    }

    [Fact]
    public void TryRecruit_SameType_MergesIntoExistingStack()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 100);
        var site = Site(("flora.scout", 10));

        Assert.True(_rules.TryRecruit(hero, site, "flora.scout", 2).Success);
        Assert.True(_rules.TryRecruit(hero, site, "flora.scout", 3).Success);

        Assert.Single(hero.Army.Stacks);
        Assert.Equal(5, hero.Army.Stacks.Single().Count);
    }

    [Fact]
    public void TryRecruit_SixthDistinctStack_IsRejectedButExistingStackCanGrow()
    {
        var offers = Enumerable.Range(1, 6).Select(index => ($"unit-{index}", 1)).ToArray();
        var hero = new HeroModel("hero", "Hero", startingGold: 100);
        var site = Site(offers);

        for (var index = 1; index <= 5; index++)
        {
            Assert.True(_rules.TryRecruit(hero, site, $"unit-{index}", 1).Success);
        }

        Assert.False(_rules.TryRecruit(hero, site, "unit-6", 1).Success);
        Assert.True(_rules.TryRecruit(hero, site, "unit-1", 1).Success);
        Assert.Equal(5, hero.Army.Stacks.Count);
        Assert.Equal(2, hero.Army.Stacks.Single(stack => stack.UnitTypeId == "unit-1").Count);
    }

    [Fact]
    public void TryRecruit_InsufficientGold_LeavesGoldArmyAndStockUnchanged()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 9);
        var site = Site(("human.guard", 10));

        var result = _rules.TryRecruit(hero, site, "human.guard", 1);

        Assert.False(result.Success);
        Assert.Equal(9, hero.Gold);
        Assert.Empty(hero.Army.Stacks);
        Assert.Equal(10, site.GetOffer("human.guard")!.RemainingStock);
    }

    [Fact]
    public void TryRecruit_UnknownTypeAndInvalidCount_AreRejected()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 100);
        var site = Site(("human.guard", 10));

        Assert.False(_rules.TryRecruit(hero, site, "undead.unknown", 1).Success);
        Assert.False(_rules.TryRecruit(hero, site, "human.guard", 0).Success);
        Assert.Equal(100, hero.Gold);
        Assert.Empty(hero.Army.Stacks);
    }

    [Fact]
    public void TryRecruit_OverflowingAdjustedPrice_IsRejectedWithoutChangingState()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: int.MaxValue, initialReputation: -3,
            factionId: "flora");
        var site = Site(("human.guard", int.MaxValue));

        var result = _rules.TryRecruit(hero, site, "human.guard", 1);

        Assert.False(result.Success);
        Assert.Equal(int.MaxValue, hero.Gold);
        Assert.Empty(hero.Army.Stacks);
    }

    private static RecruitmentSite Site(params (string UnitTypeId, int BaseUnitCost)[] offers) =>
        new("village", offers.Select(offer => new RecruitmentOffer(offer.UnitTypeId, offer.BaseUnitCost, 10)), "flora");
}
