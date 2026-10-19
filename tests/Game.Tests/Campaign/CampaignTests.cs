using Game.Core.Campaigns;
using Game.Core.Characters;
using Game.Core.Combat;
using Game.Core.Geometry;
using Game.Core.Economy;
using Game.Core.Persistence;
using Game.Core.World;
using HeroModel = Game.Core.Characters.Hero;
using Xunit;

namespace Game.Tests.Campaigns;

public sealed class CampaignTests
{
    [Fact]
    public void RecruitmentSite_CanOnlyBeOpenedAfterTwoClickMovementReachesItsPoint()
    {
        var start = new HexCoord(0, 0);
        var destination = new HexCoord(1, 0);
        var point = new WorldPointOfInterest("greenhouse", "recruitment", "Greenhouse", destination, "greenhouse");
        var site = new RecruitmentSite("greenhouse", [new RecruitmentOffer("flora.guard", 10, 3)]);
        var campaign = new Campaign(new HeroModel("hero", "Hero", 100), [], [site]);
        var movement = new MovementSelection(new BattleMap([
            new BattleTile(start), new BattleTile(destination)]), start);

        Assert.False(movement.Select(destination));
        Assert.False(campaign.TryGetRecruitmentSiteAt(point, movement.Position, out _));
        Assert.True(movement.Select(destination));
        Assert.True(campaign.TryGetRecruitmentSiteAt(point, movement.Position, out var openedSite));
        Assert.Same(site, openedSite);
    }

    [Fact]
    public void RecruitmentStockSnapshot_IncludesEveryOfferIncludingDepletedOnes()
    {
        var site = new RecruitmentSite("greenhouse", [new RecruitmentOffer("flora.guard", 10, 3)]);
        var hero = new HeroModel("hero", "Hero", startingGold: 100);
        var campaign = new Campaign(hero, [], [site]);
        Assert.True(new EconomyRules().TryRecruit(hero, site, "flora.guard", 3).Success);

        var snapshot = campaign.GetRecruitmentStockSnapshot();

        Assert.Equal([new SavedRecruitmentStock("greenhouse", "flora.guard", 0)], snapshot);
    }

    [Fact]
    public void RestoreRecruitmentStock_RejectsIncompleteSetWithoutChangingAnyOffer()
    {
        var first = new RecruitmentSite("first", [new RecruitmentOffer("flora.a", 10, 3)]);
        var second = new RecruitmentSite("second", [new RecruitmentOffer("flora.b", 12, 4)]);
        var campaign = new Campaign(new HeroModel("hero", "Hero"), [], [first, second]);

        var restored = campaign.TryRestoreRecruitmentStock([
            new SavedRecruitmentStock("first", "flora.a", 1)]);

        Assert.False(restored.Success);
        Assert.Equal(3, first.GetOffer("flora.a")!.RemainingStock);
        Assert.Equal(4, second.GetOffer("flora.b")!.RemainingStock);
    }

    [Fact]
    public void RestoreRecruitmentStock_AppliesCompleteValidatedSetIncludingZero()
    {
        var first = new RecruitmentSite("first", [new RecruitmentOffer("flora.a", 10, 3)]);
        var second = new RecruitmentSite("second", [new RecruitmentOffer("flora.b", 12, 4)]);
        var campaign = new Campaign(new HeroModel("hero", "Hero"), [], [first, second]);

        var restored = campaign.TryRestoreRecruitmentStock([
            new SavedRecruitmentStock("first", "flora.a", 0),
            new SavedRecruitmentStock("second", "flora.b", 2)]);

        Assert.True(restored.Success, restored.Message);
        Assert.Equal(0, first.GetOffer("flora.a")!.RemainingStock);
        Assert.Equal(2, second.GetOffer("flora.b")!.RemainingStock);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("negative")]
    [InlineData("unknown")]
    [InlineData("excess")]
    public void RestoreRecruitmentStock_RejectsInvalidSetWithoutPartialChanges(string kind)
    {
        var first = new RecruitmentSite("first", [new RecruitmentOffer("flora.a", 10, 3)]);
        var second = new RecruitmentSite("second", [new RecruitmentOffer("flora.b", 12, 4)]);
        var campaign = new Campaign(new HeroModel("hero", "Hero"), [], [first, second]);
        SavedRecruitmentStock[] stocks = kind switch
        {
            "duplicate" => [new("first", "flora.a", 1), new("first", "flora.a", 2), new("second", "flora.b", 2)],
            "negative" => [new("first", "flora.a", -1), new("second", "flora.b", 2)],
            "excess" => [new("first", "flora.a", 4), new("second", "flora.b", 2)],
            _ => [new("first", "flora.a", 1), new("missing", "flora.b", 2)]
        };

        var restored = campaign.TryRestoreRecruitmentStock(stocks);

        Assert.False(restored.Success);
        Assert.Equal(3, first.GetOffer("flora.a")!.RemainingStock);
        Assert.Equal(4, second.GetOffer("flora.b")!.RemainingStock);
    }

    [Fact]
    public void ApplyChoice_AppliesGoldAndReputationOnce()
    {
        var hero = new HeroModel("flora-hero", "Keeper", startingGold: 20);
        var quest = new Quest("spring", "Spring source", new[]
        {
            new QuestChoice("share-water", "Share the water", goldChange: 15, reputationChange: 2),
            new QuestChoice("keep-water", "Keep the water", goldChange: 25, reputationChange: -1)
        });
        var campaign = new Campaign(hero, new[] { quest });

        var result = campaign.ApplyChoice("spring", "share-water");

        Assert.True(result.Success);
        Assert.Equal(35, hero.Gold);
        Assert.Equal(2, hero.Reputation);
        Assert.Equal("share-water", campaign.SelectedChoiceIds["spring"]);
        Assert.False(campaign.ApplyChoice("spring", "keep-water").Success);
        Assert.Equal(35, hero.Gold);
        Assert.Equal(2, hero.Reputation);
    }

    [Fact]
    public void ApplyChoice_UnknownQuestOrChoice_ReturnsFailureWithoutChanges()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 10);
        var campaign = new Campaign(hero,
            new[] { new Quest("quest", "Quest", new[] { new QuestChoice("choice", "Choice", 50, 1) }) });

        Assert.False(campaign.ApplyChoice("missing", "choice").Success);
        Assert.False(campaign.ApplyChoice("quest", "missing").Success);
        Assert.Equal(10, hero.Gold);
        Assert.Equal(0, hero.Reputation);
    }

    [Fact]
    public void ApplyChoice_CannotSpendMoreGoldThanHeroHas_AndQuestRemainsAvailable()
    {
        var hero = new HeroModel("hero", "Hero", startingGold: 5);
        var quest = new Quest("toll", "Bridge toll", new[]
        {
            new QuestChoice("pay", "Pay the toll", goldChange: -10, reputationChange: 2),
            new QuestChoice("refuse", "Refuse the toll", goldChange: 0, reputationChange: -1)
        });
        var campaign = new Campaign(hero, new[] { quest });

        Assert.False(campaign.ApplyChoice("toll", "pay").Success);
        Assert.Empty(campaign.CompletedQuestIds);
        Assert.Equal(5, hero.Gold);
        Assert.Equal(0, hero.Reputation);
        Assert.True(campaign.ApplyChoice("toll", "refuse").Success);
    }

    [Fact]
    public void CompletedQuestIds_ReturnsSnapshotThatCannotChangeCampaignState()
    {
        var hero = new HeroModel("hero", "Hero");
        var campaign = new Campaign(hero, new[]
        {
            new Quest("test", "Test", new[] { new QuestChoice("done", "Done", 0, 0) })
        });
        campaign.ApplyChoice("test", "done");
        var snapshot = (HashSet<string>)campaign.CompletedQuestIds;

        snapshot.Clear();

        Assert.False(campaign.ApplyChoice("test", "done").Success);
        Assert.Contains("test", campaign.CompletedQuestIds);
    }
}
