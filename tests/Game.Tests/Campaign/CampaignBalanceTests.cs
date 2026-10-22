using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Combat.Pathfinding;
using Game.Core.Content;
using Game.Core.Economy;
using Game.Core.Navigation;
using Game.Core.Persistence;
using Xunit;

namespace Game.Tests.Campaigns;

public sealed class CampaignBalanceTests
{
    [Fact]
    public void IslandTravelKeepsVisibleOffRoadLandImpassable()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        var marked = definition.Map.PointsOfInterest.Select(point => point.Coordinate).ToHashSet();
        var land = definition.Map.Tiles.First(tile => tile.IsPassable && !tile.IsRoad &&
            !marked.Contains(tile.Coordinate) && tile.Coordinate != definition.Map.StartPosition);

        Assert.False(campaign.CreateTravelMap(definition.Map, definition.Map.StartPosition!.Value)
            .GetTile(land.Coordinate)!.IsPassable);
    }

    [Theory]
    [InlineData("help", "plants-pasture-battle", true)]
    [InlineData("refuse", "farmers-pasture-battle", true)]
    [InlineData("help", "plants-pasture-battle", false)]
    [InlineData("refuse", "farmers-pasture-battle", false)]
    [InlineData(null, null, false)]
    public void MandatoryCampaignCanBeWonWithStartingGoldAndNoOptionalBattles(
        string? choiceId, string? firstEncounterId, bool clearNorthernForest)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        var position = definition.Map.StartPosition!.Value;
        var navigation = definition.Map.Navigation!;
        var artPosition = navigation.Start;
        var pathfinder = new AStarPathfinder();
        var economy = new EconomyRules();
        var introduction = campaign.Quests.Single(quest => quest.Id == "guide-introduction");
        Assert.True(campaign.ApplyChoice(introduction.Id, introduction.Choices[0].Id).Success);
        TravelTo("starting-clearing");
        var startingSite = campaign.RecruitmentSites.Single(site => site.Id == "starting-clearing");
        Assert.True(economy.TryRecruit(campaign.Hero, startingSite, "flora.thorn-guard", 18).Success);
        Assert.True(economy.TryRecruit(campaign.Hero, startingSite, "flora.sprout", 8).Success);
        Assert.Equal(0, campaign.Hero.Gold);
        Assert.False(PathTo("burned-forest").IsReachable);
        if (choiceId is not null)
        {
            TravelTo("pasture-request");
            Assert.True(campaign.ApplyChoice("pasture-choice", choiceId).Success);
            ReloadProgress();
            Win(firstEncounterId!);
        }
        if (clearNorthernForest)
        {
            Win("burned-forest");
            Recruit("flora-tier-two", "flora.ember-bloom");
            Recruit("flora-tier-two", "flora.pod-slinger");
        }
        Win("mountain-outpost");
        Win("mushroom-outpost");
        Recruit("flora-tier-three", "flora.root-warden", 3);
        Win("final-landing");

        Assert.True(campaign.IsVictory);
        Assert.DoesNotContain(campaign.CompletedEncounterIds, id => id.StartsWith("artifact-", StringComparison.Ordinal));

        void Recruit(string siteId, string unitId, int count = 1)
        {
            Assert.True(campaign.CanAccessRecruitmentSite(siteId), $"Recruitment site '{siteId}' is not unlocked.");
            var site = campaign.RecruitmentSites.Single(candidate => candidate.Id == siteId);
            TravelTo(definition.Map.PointsOfInterest.Single(point => point.RecruitmentSiteId == siteId).Id);
            var result = economy.TryRecruit(campaign.Hero, site, unitId, count);
            Assert.True(result.Success, result.Message);
            ReloadProgress();
        }

        void Win(string encounterId)
        {
            var encounter = campaign.Encounters.Single(candidate => candidate.Id == encounterId);
            var point = definition.Map.PointsOfInterest.Single(candidate => candidate.Id == encounter.PointOfInterestId);
            TravelTo(point.Id);
            var session = campaign.TryStartEncounter(point, point.Coordinate, units).Session;
            Assert.NotNull(session);
            SimulateBattle(session!.Battle);
            Assert.Equal(BattleSide.Player, session.Battle.Winner);
            var resolution = campaign.ResolveEncounter(session);
            Assert.True(resolution.Success, resolution.Message);
            Assert.True(resolution.Victory);
            ReloadProgress();
        }

        void TravelTo(string pointId)
        {
            var path = PathTo(pointId);
            Assert.True(path.IsReachable, $"Cannot travel from {position} to '{pointId}'.");
            var travel = new AdventureTravel(new(navigation, campaign.IsUnlocked), artPosition);
            if (!travel.CanInteractWith(navigation.Points[pointId]))
            {
                travel.SelectApproach(navigation.Points[pointId]);
                Assert.True(travel.SelectApproach(navigation.Points[pointId]),
                    $"Real island navigation cannot approach '{pointId}' from {artPosition}.");
                Assert.True(travel.Advance(1000));
            }
            Assert.True(travel.CanInteractWith(navigation.Points[pointId]));
            artPosition = travel.Position;
            position = definition.Map.PointsOfInterest.Single(point => point.Id == pointId).Coordinate;
        }

        void ReloadProgress()
        {
            var save = campaign.CreateSave(definition.Id, definition.Map.Id, position) with
                { AdventurePosition = artPosition };
            var path = Path.Combine(Path.GetTempPath(), $"flora-playthrough-{Guid.NewGuid():N}.json");
            try
            {
                Assert.True(CampaignSerializer.Save(path, save).Success);
                var loaded = CampaignSerializer.Load(path);
                Assert.True(loaded.Success);
                var restored = definition.CreateGameState();
                Assert.True(restored.TryRestoreSave(loaded.Save!, definition.Id, definition.PlayerFactionId,
                    definition.Map, units, out var error), error);
                Assert.Equal(campaign.Hero.Gold, restored.Hero.Gold);
                Assert.Equal(campaign.CompletedEncounterIds.Order(), restored.CompletedEncounterIds.Order());
                campaign = restored;
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        PathResult PathTo(string pointId)
        {
            var destination = definition.Map.PointsOfInterest.Single(point => point.Id == pointId).Coordinate;
            return pathfinder.FindPath(campaign.CreateTravelMap(definition.Map, position), position, destination);
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
