using System.Reflection;
using Game.Core.Campaigns;
using Game.Core.Characters;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Economy;
using Game.Core.World;
using HeroModel = Game.Core.Characters.Hero;
using Xunit;

namespace Game.Tests.Campaigns;

public sealed class ProgressionTests
{
    private static readonly WorldPointOfInterest Peasants = new("peasants", "battle", "Peasants", new(1, 1));
    private static readonly WorldPointOfInterest North = new("north", "battle", "North", new(2, 2));
    private static readonly WorldPointOfInterest SitePoint = new("tier-two", "recruitment", "T2", new(3, 3), "tier-two");
    private static UnitCatalog Units() => UnitCatalog.Load(Path.Combine(AppContext.BaseDirectory,
        "content", "units", "units.json")).Catalog!;

    private static Campaign Create(bool recruit = true)
    {
        var hero = new HeroModel("hero", "Dryad", 100);
        if (recruit)
            Assert.True(new EconomyRules().TryRecruit(hero,
                new RecruitmentSite("start", [new RecruitmentOffer("flora.thorn-guard", 1, 6)]),
                "flora.thorn-guard", 6).Success);
        return new Campaign(hero,
            [new Quest("peasants", "Peasants", [new QuestChoice("help", "Help", 0, 0),
                new QuestChoice("refuse", "Refuse", 0, 0)])],
            [new RecruitmentSite("tier-two", [new RecruitmentOffer("flora.ember-bloom", 5, 3)])],
            [new CampaignEncounter("peasants-refused", Peasants.Id, "Peasants", 123,
                [new EncounterTroop("human.militia", 1)], 10, 10),
             new CampaignEncounter("north-knights", North.Id, "North", 456,
                [new EncounterTroop("human.militia", 1)], 0, 0)],
            new CampaignProgressionRules(
                encounterRequirements: new Dictionary<string, CampaignAccessRequirement>
                {
                    ["peasants-refused"] = new([], "peasants", "refuse"),
                    ["north-knights"] = new(["peasants-refused"])
                },
                recruitmentRequirements: new Dictionary<string, CampaignAccessRequirement>
                {
                    ["tier-two"] = new(["north-knights"])
                },
                unlockEncounterIds: new Dictionary<string, string> { ["north-key"] = "peasants-refused" },
                encounterReputationRewards: new Dictionary<string, int> { ["peasants-refused"] = 1 },
                victoryEncounterId: "north-knights", defeatOnArmyWipe: true));
    }

    private static void KillSide(CampaignBattle session, BattleSide side)
    {
        var damage = typeof(BattleUnit).GetMethod("ApplyDamage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var unit in session.Battle.Units.Where(unit => unit.Side == side))
            damage.Invoke(unit, [long.MaxValue]);
    }

    [Fact]
    public void ChoiceAndVictoryGateKeyRecruitmentAndFinalWin()
    {
        var campaign = Create();
        Assert.False(campaign.CanAccessEncounter("peasants-refused"));
        Assert.False(campaign.IsUnlocked("north-key"));
        Assert.False(campaign.TryGetRecruitmentSiteAt(SitePoint, SitePoint.Coordinate, out _));
        Assert.True(campaign.ApplyChoice("peasants", "refuse").Success);
        Assert.Equal(0, campaign.Hero.Reputation);
        Assert.False(campaign.CanAccessEncounter("north-knights"));
        Assert.True(campaign.CanAccessEncounter("peasants-refused"));
        var first = campaign.TryStartEncounter(Peasants, Peasants.Coordinate, Units()).Session!;
        KillSide(first, BattleSide.Enemy);
        Assert.True(campaign.ResolveEncounter(first).Victory);
        Assert.True(campaign.IsUnlocked("north-key"));
        Assert.Equal(1, campaign.Hero.Reputation);
        Assert.True(campaign.CanAccessEncounter("north-knights"));
        Assert.False(campaign.CanAccessRecruitmentSite("tier-two"));
        var second = campaign.TryStartEncounter(North, North.Coordinate, Units()).Session!;
        KillSide(second, BattleSide.Enemy);
        Assert.True(campaign.ResolveEncounter(second).Victory);
        Assert.True(campaign.IsVictory);
        Assert.False(campaign.CanAccessEncounter("north-knights"));
    }

    [Fact]
    public void WipedArmyEndsCampaignAndRestoresWithoutReopeningEvents()
    {
        var campaign = Create();
        Assert.True(campaign.ApplyChoice("peasants", "refuse").Success);
        var session = campaign.TryStartEncounter(Peasants, Peasants.Coordinate, Units()).Session!;
        KillSide(session, BattleSide.Player);
        Assert.False(campaign.ResolveEncounter(session).Victory);
        Assert.True(campaign.IsDefeated);
        Assert.False(campaign.CanAccessEncounter("peasants-refused"));
        Assert.False(campaign.ApplyChoice("peasants", "help").Success);
        var fresh = Create(recruit: false);
        Assert.True(fresh.TryRestoreProgress(campaign.GetProgressSnapshot()));
        Assert.True(fresh.IsDefeated);
    }

    [Fact]
    public void InvalidProgressDoesNotPartiallyRestore()
    {
        var campaign = Create();
        Assert.False(campaign.TryRestoreProgress(new CampaignProgressSnapshot(["peasants-refused", "unknown"], false)));
        Assert.Empty(campaign.CompletedEncounterIds);
        Assert.False(campaign.TryRestoreProgress(new CampaignProgressSnapshot(["peasants-refused"], true)));
        Assert.Empty(campaign.CompletedEncounterIds);
    }
}
