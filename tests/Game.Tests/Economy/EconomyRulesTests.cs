using Game.Core.Economy;
using Game.Core.Characters;
using HeroModel = Game.Core.Characters.Hero;
using Xunit;

namespace Game.Tests.Economy;

public sealed class EconomyRulesTests
{
    private readonly EconomyRules _rules = new();

    [Theory]
    [InlineData(2, 80)]
    [InlineData(-2, 120)]
    public void TryRecruit_ReputationChangesPriceWithoutBlockingAccess(int reputation, int expectedUnitPrice)
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 500, initialReputation: reputation);
        var site = Site(("flora.scout", 100));

        var result = _rules.TryRecruit(hero, site, "flora.scout", 2);

        Assert.True(result.Success);
        Assert.Equal(expectedUnitPrice * 2, result.TotalCost);
        Assert.Equal(500 - expectedUnitPrice * 2, hero.Gold);
        Assert.Equal(2, hero.Army.Stacks.Single().Count);
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
    public void TryRecruit_InsufficientGold_LeavesGoldAndArmyUnchanged()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 9);
        var site = Site(("human.guard", 10));

        var result = _rules.TryRecruit(hero, site, "human.guard", 1);

        Assert.False(result.Success);
        Assert.Equal(9, hero.Gold);
        Assert.Empty(hero.Army.Stacks);
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
        var hero = new HeroModel("hero", "Hero", startingGold: int.MaxValue, initialReputation: -3);
        var site = Site(("human.guard", int.MaxValue));

        var result = _rules.TryRecruit(hero, site, "human.guard", 1);

        Assert.False(result.Success);
        Assert.Equal(int.MaxValue, hero.Gold);
        Assert.Empty(hero.Army.Stacks);
    }

    private static RecruitmentSite Site(params (string UnitTypeId, int BaseUnitCost)[] offers) =>
        new("village", offers.Select(offer => new RecruitmentOffer(offer.UnitTypeId, offer.BaseUnitCost)));
}
