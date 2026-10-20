using System.Reflection;
using Game.Core.Campaigns;
using Game.Core.Characters;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Economy;
using Game.Core.World;
using Game.Core.Geometry;
using Game.Core.Combat.Pathfinding;
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

    [Fact]
    public void BranchUnlockOpensWhenEitherAllowedBattleIsWon()
    {
        var campaign = new Campaign(new HeroModel("hero", "Dryad"), [], encounters:
        [
            new CampaignEncounter("help-route", "help-point", "Plants", 1,
                [new EncounterTroop("human.militia", 1)], 0, 0),
            new CampaignEncounter("refuse-route", "refuse-point", "Farmers", 2,
                [new EncounterTroop("human.militia", 1)], 0, 0)
        ], progression: new CampaignProgressionRules(unlockEncounterOptions:
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["north-gate"] = ["help-route", "refuse-route"]
            }));

        Assert.False(campaign.IsUnlocked("north-gate"));
        Assert.True(campaign.TryRestoreProgress(new CampaignProgressSnapshot(["help-route"], false)));
        Assert.True(campaign.IsUnlocked("north-gate"));
    }

    [Theory]
    [InlineData("help", "plants-pasture-battle", "farmers-pasture-battle", 0, true)]
    [InlineData("refuse", "farmers-pasture-battle", "plants-pasture-battle", 1, false)]
    public void FloraCampaignBranchUnlocksNorthOnlyAfterWinningAndGatesRecruitment(
        string choiceId, string chosenEncounter, string rejectedEncounter, int expectedReputation,
        bool peasantsCanBeRecruited)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        var start = definition.Map.StartPosition!.Value;
        var north = definition.Map.PointsOfInterest.Single(point => point.Id == "burned-forest").Coordinate;

        Assert.False(new AStarPathfinder().FindPath(campaign.CreateTravelMap(definition.Map, start), start, north).IsReachable);
        Assert.False(campaign.IsUnlocked("north-gate"));
        Assert.True(campaign.ApplyChoice("pasture-choice", choiceId).Success);
        Assert.Equal(chosenEncounter, campaign.FindEncounterPointAt(definition.Map,
            definition.Map.PointsOfInterest.Single(point => point.Id == "pasture-request").Coordinate)?.Id);
        Assert.True(campaign.CanAccessEncounter(chosenEncounter));
        Assert.False(campaign.CanAccessEncounter(rejectedEncounter));
        Assert.False(campaign.CanAccessRecruitmentSite("peasant-recruitment"));
        Assert.False(campaign.CanAccessEncounter("burned-forest"));

        var encounter = campaign.Encounters.Single(candidate => candidate.Id == chosenEncounter);
        var point = definition.Map.PointsOfInterest.Single(candidate => candidate.Id == encounter.PointOfInterestId);
        var session = campaign.TryStartEncounter(point, point.Coordinate, units).Session!;
        KillSide(session, BattleSide.Enemy);
        Assert.True(campaign.ResolveEncounter(session).Victory);

        Assert.True(campaign.IsUnlocked("north-gate"));
        Assert.Equal(expectedReputation, campaign.Hero.Reputation);
        Assert.Equal(peasantsCanBeRecruited, campaign.CanAccessRecruitmentSite("peasant-recruitment"));
        Assert.True(new AStarPathfinder().FindPath(campaign.CreateTravelMap(definition.Map, start), start, north).IsReachable);
        Assert.True(campaign.CanAccessEncounter("burned-forest"));
        Assert.False(campaign.CanAccessEncounter("mountain-outpost"));

        var previousPosition = point.Coordinate;
        WinReachableEncounter("burned-forest");
        Assert.True(campaign.CanAccessRecruitmentSite("flora-tier-two"));
        Assert.False(campaign.CanAccessEncounter("mushroom-outpost"));
        WinReachableEncounter("mountain-outpost");
        Assert.True(campaign.CanAccessEncounter("mushroom-outpost"));
        Assert.False(campaign.CanAccessRecruitmentSite("flora-tier-three"));
        WinReachableEncounter("mushroom-outpost");
        Assert.True(campaign.CanAccessRecruitmentSite("flora-tier-three"));
        Assert.True(campaign.CanAccessEncounter("final-landing"));
        WinReachableEncounter("final-landing");
        Assert.True(campaign.IsVictory);

        var lateSave = campaign.CreateSave(definition.Id, definition.Map.Id, previousPosition);
        var restoredCampaign = definition.CreateGameState();
        Assert.True(restoredCampaign.TryRestoreSave(lateSave, definition.Id, definition.PlayerFactionId,
            definition.Map, units, out var restoreError), restoreError);
        Assert.True(restoredCampaign.IsVictory);
        Assert.Equal(campaign.CompletedEncounterIds, restoredCampaign.CompletedEncounterIds);
        Assert.True(restoredCampaign.IsUnlocked("north-gate"));

        void WinReachableEncounter(string encounterId)
        {
            var next = campaign.Encounters.Single(candidate => candidate.Id == encounterId);
            var nextPoint = definition.Map.PointsOfInterest.Single(candidate => candidate.Id == next.PointOfInterestId);
            Assert.True(new AStarPathfinder().FindPath(campaign.CreateTravelMap(definition.Map, previousPosition),
                previousPosition, nextPoint.Coordinate).IsReachable);
            var nextBattle = campaign.TryStartEncounter(nextPoint, nextPoint.Coordinate, units).Session!;
            KillSide(nextBattle, BattleSide.Enemy);
            Assert.True(campaign.ResolveEncounter(nextBattle).Victory);
            previousPosition = nextPoint.Coordinate;
        }
    }

    [Fact]
    public void FloraTravelMapAllowsRoadsAndMarkedPointsButBlocksOpenLandShortcuts()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var campaign = definition.CreateGameState();
        var start = definition.Map.StartPosition!.Value;
        var travelMap = campaign.CreateTravelMap(definition.Map, start);
        var markedPoint = definition.Map.PointsOfInterest.Single(point => point.Id == "starting-clearing");
        var openLand = definition.Map.Tiles.First(tile => tile.IsPassable && !tile.IsRoad &&
            !definition.Map.PointsOfInterest.Any(point => point.Coordinate == tile.Coordinate));

        Assert.True(travelMap.GetTile(start)!.IsPassable);
        Assert.True(travelMap.GetTile(markedPoint.Coordinate)!.IsPassable);
        Assert.False(travelMap.GetTile(openLand.Coordinate)!.IsPassable);

        var roadGraph = new BattleMap(definition.Map.Tiles.Select(tile => new BattleTile(tile.Coordinate,
            tile.Height, tile.IsPassable && (tile.IsRoad || definition.Map.PointsOfInterest.Any(point =>
                point.Coordinate == tile.Coordinate)))));
        foreach (var point in definition.Map.PointsOfInterest)
            Assert.True(new AStarPathfinder().FindPath(roadGraph, start, point.Coordinate).IsReachable,
                $"The marked road network must reach {point.Id}.");
    }
}
