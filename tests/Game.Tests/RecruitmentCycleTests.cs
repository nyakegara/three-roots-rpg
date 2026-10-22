using Game.Core.Characters;
using Game.Core.Economy;
using Xunit;
namespace Game.Tests;
public sealed class RecruitmentCycleTests
{
    [Fact]
    public void SuccessfulPurchaseRestartsSiteClockButFailureDoesNot()
    {
        var hero = new Game.Core.Characters.Hero("h", "Hero", 20);
        var site = new RecruitmentSite("site", [new RecruitmentOffer("unit", 1, 5)]);
        var rules = new EconomyRules();
        Assert.True(rules.TryRecruit(hero, site, "unit", 1).Success);
        typeof(RecruitmentSite).GetMethod("CompleteBattle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(site, null);
        Assert.Equal(2, site.BattlesUntilRestock);
        Assert.False(rules.TryRecruit(hero, site, "unit", 99).Success);
        Assert.Equal(2, site.BattlesUntilRestock);
        Assert.True(rules.TryRecruit(hero, site, "unit", 1).Success);
        Assert.Equal(3, site.BattlesUntilRestock);
    }
}
