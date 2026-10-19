using Game.Core.Campaigns;
using Game.Core.Characters;
using HeroModel = Game.Core.Characters.Hero;
using Xunit;

namespace Game.Tests.Campaigns;

public sealed class CampaignTests
{
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
