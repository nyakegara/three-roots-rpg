using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Content;
using Game.Core.Economy;
using Xunit;

namespace Game.Tests.Campaigns;

public sealed class CampaignBalanceTests
{
    [Theory]
    [InlineData("help", "plants-pasture-battle")]
    [InlineData("refuse", "farmers-pasture-battle")]
    public void MandatoryCampaignCanBeWonWithStartingGoldAndNoOptionalBattles(string choiceId, string firstEncounterId)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        var economy = new EconomyRules();
        var startingSite = campaign.RecruitmentSites.Single(site => site.Id == "starting-clearing");
        Assert.True(economy.TryRecruit(campaign.Hero, startingSite, "flora.thorn-guard", 18).Success);
        Assert.True(economy.TryRecruit(campaign.Hero, startingSite, "flora.sprout", 8).Success);
        Assert.Equal(0, campaign.Hero.Gold);
        Assert.True(campaign.ApplyChoice("pasture-choice", choiceId).Success);

        Win(firstEncounterId);
        Win("burned-forest");
        Recruit("flora-tier-two", "flora.ember-bloom");
        Recruit("flora-tier-two", "flora.pod-slinger");
        Win("mountain-outpost");
        Win("mushroom-outpost");
        Recruit("flora-tier-three", "flora.root-warden");
        Win("final-landing");

        Assert.True(campaign.IsVictory);
        Assert.DoesNotContain(campaign.CompletedEncounterIds, id => id.StartsWith("artifact-", StringComparison.Ordinal));

        void Recruit(string siteId, string unitId)
        {
            Assert.True(campaign.CanAccessRecruitmentSite(siteId), $"Recruitment site '{siteId}' is not unlocked.");
            var site = campaign.RecruitmentSites.Single(candidate => candidate.Id == siteId);
            var result = economy.TryRecruit(campaign.Hero, site, unitId, 1);
            Assert.True(result.Success, result.Message);
        }

        void Win(string encounterId)
        {
            var encounter = campaign.Encounters.Single(candidate => candidate.Id == encounterId);
            var point = definition.Map.PointsOfInterest.Single(candidate => candidate.Id == encounter.PointOfInterestId);
            var session = campaign.TryStartEncounter(point, point.Coordinate, units).Session;
            Assert.NotNull(session);
            SimulateBattle(session!.Battle);
            Assert.Equal(BattleSide.Player, session.Battle.Winner);
            var resolution = campaign.ResolveEncounter(session);
            Assert.True(resolution.Success, resolution.Message);
            Assert.True(resolution.Victory);
        }
    }

    private static void SimulateBattle(Battle battle)
    {
        var ai = new BattleAi();
        battle.StartRound();
        for (var step = 0; step < 10000 && !battle.IsFinished; step++)
        {
            var active = battle.ActiveUnit;
            if (active is null)
            {
                battle.StartRound();
                continue;
            }

            if (active.Ability == UnitAbility.HealAlly && !battle.HasAttacked(active.Id))
            {
                var wounded = battle.Units.Where(unit => unit.IsAlive && unit.Side == active.Side && unit.Id != active.Id &&
                        unit.RemainingHitPoints < unit.MaximumHitPointsAtBattleStart)
                    .OrderBy(unit => unit.RemainingHitPoints)
                    .FirstOrDefault();
                if (wounded is not null && battle.TryHeal(active.Id, wounded.Id))
                    continue;
            }

            var action = ai.ChooseAction(battle, active.Id);
            var applied = action.Type switch
            {
                BattleActionType.Move => battle.Move(active.Id, action.Destination!.Value),
                BattleActionType.Attack => battle.Attack(active.Id, action.TargetId!),
                BattleActionType.SetAttackMode => battle.TrySetAttackMode(active.Id, action.Mode!.Value),
                _ => EndActivation()
            };
            Assert.True(applied, $"AI action {action.Type} was rejected for {active.Id}.");

            bool EndActivation()
            {
                battle.EndActivation();
                return true;
            }
        }

        Assert.True(battle.IsFinished, "The battle did not finish within 10,000 AI actions.");
    }
}
