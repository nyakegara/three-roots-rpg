using System.Reflection;
using Game.Core.Campaigns;
using HeroModel = Game.Core.Characters.Hero;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Economy;
using Game.Core.Geometry;
using Game.Core.World;
using Xunit;

namespace Game.Tests.Campaigns;

public sealed class EncounterTests
{
    private static readonly WorldPointOfInterest Point = new("iron-road", "battle", "Road", new(2, 3));
    private static UnitCatalog Catalog() => UnitCatalog.Load(Path.Combine(AppContext.BaseDirectory,
        "content", "units", "units.json")).Catalog!;
    private static CampaignEncounter Encounter(string id = "road", int gold = 20, int xp = 30) =>
        new(id, Point.Id, "Road guards", 123, [new("human.militia", 2)], gold, xp);
    private static Campaign Create(int count = 4, int gold = 20, int xp = 30)
    {
        var hero = new HeroModel("hero", "Hero", 100);
        if (count > 0) Recruit(hero, "flora.thorn-guard", count);
        return new Campaign(hero, [], encounters: [Encounter(gold: gold, xp: xp)]);
    }
    private static void Recruit(HeroModel hero, string type, int count) => Assert.True(new EconomyRules()
        .TryRecruit(hero, new RecruitmentSite("site", [new RecruitmentOffer(type, 1, count)]), type, count).Success);
    private static CampaignBattle Start(Campaign campaign) => Assert.IsType<CampaignBattle>(
        campaign.TryStartEncounter(Point, Point.Coordinate, Catalog()).Session);
    private static void Damage(BattleUnit unit, long amount) => typeof(BattleUnit)
        .GetMethod("ApplyDamage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(unit, [amount]);
    private static void Convert(BattleUnit unit, BattleSide side) => typeof(BattleUnit)
        .GetMethod("ConvertTo", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(unit, [side]);
    private static void Win(CampaignBattle session)
    {
        foreach (var enemy in session.Battle.Units.Where(u => u.OriginalSide == BattleSide.Enemy))
            Damage(enemy, long.MaxValue);
    }

    [Fact]
    public void Encounter_KeepsRepeatedTypesAsDistinctStacks()
    {
        var encounter = new CampaignEncounter("road", Point.Id, "Guards", 123,
            [new("human.militia", 1), new("human.militia", 2)], 20, 30);
        Assert.Equal(new[] { 1, 2 }, encounter.EnemyArmy.Select(t => t.Count));
        var hero = new HeroModel("hero", "Hero", 100);
        Recruit(hero, "flora.thorn-guard", 4);
        var campaign = new Campaign(hero, [], encounters: [encounter]);
        var enemies = Start(campaign).Battle.Units.Where(u => u.Side == BattleSide.Enemy).ToArray();
        Assert.Equal(new[] { 1, 2 }, enemies.Select(u => u.Count));
        Assert.Equal(2, enemies.Select(u => u.Id).Distinct().Count());
        Assert.Equal(2, enemies.Select(u => u.Position).Distinct().Count());
    }

    [Fact]
    public void Start_RequiresMatchingLocationNonemptyArmyAndNoActiveSession()
    {
        var campaign = Create();
        Assert.False(campaign.TryStartEncounter(Point, new HexCoord(0, 0), Catalog()).Success);
        Assert.False(campaign.TryStartEncounter(new("elsewhere", "battle", "Elsewhere", Point.Coordinate),
            Point.Coordinate, Catalog()).Success);
        Assert.False(Create(0).TryStartEncounter(Point, Point.Coordinate, Catalog()).Success);
        Assert.True(campaign.TryStartEncounter(Point, Point.Coordinate, Catalog()).Success);
        Assert.False(campaign.TryStartEncounter(Point, Point.Coordinate, Catalog()).Success);
    }

    [Fact]
    public void Start_UsesHiredCountsCatalogStatsAbilitiesAndSafeDistinctPositions()
    {
        var campaign = Create();
        Recruit(campaign.Hero, "flora.sprout", 2);
        var session = Start(campaign);
        var player = session.Battle.Units.Where(u => u.Side == BattleSide.Player).ToArray();
        Assert.Equal(2, player.Length);
        var guard = Assert.Single(player, u => u.UnitTypeId == "flora.thorn-guard");
        Assert.Equal(4, guard.Count);
        Assert.Equal(40, guard.RemainingHitPoints);
        Assert.Equal(UnitAbility.HealAlly, Assert.Single(player, u => u.UnitTypeId == "flora.sprout").Ability);
        Assert.Equal(session.Battle.Units.Count, session.Battle.Units.Select(u => u.Position).Distinct().Count());
        Assert.All(session.Battle.Units, u => Assert.True(session.Field.Map.GetTile(u.Position)!.IsPassable));
    }

    [Fact]
    public void Start_FiveStacksPerSideUseSafeDistinctStaggeredDeploymentCells()
    {
        var hero = new HeroModel("hero", "Hero", 100);
        foreach (var type in new[] { "flora.sprout", "flora.thorn-guard", "flora.ember-bloom", "flora.pod-slinger", "flora.root-warden" })
            Recruit(hero, type, 1);
        var encounter = new CampaignEncounter("road", Point.Id, "Guards", 123,
            [new("human.militia", 1), new("human.spearman", 1), new("human.longbow", 1),
                new("human.field-medic", 1), new("human.banner-knight", 1)], 20, 30);
        var session = Start(new Campaign(hero, [], encounters: [encounter]));
        var units = session.Battle.Units.ToArray();

        Assert.Equal(10, units.Select(unit => unit.Position).Distinct().Count());
        Assert.All(units, unit => Assert.True(session.Field.Map.GetTile(unit.Position)!.IsPassable));
        foreach (var side in new[] { BattleSide.Player, BattleSide.Enemy })
        {
            var positions = units.Where(unit => unit.Side == side).Select(unit => unit.Position).ToArray();
            Assert.Equal(2, positions.Select(position => position.Q + position.R / 2).Distinct().Count());
            Assert.Equal(5, positions.Select(position => position.R).Distinct().Count());
        }
    }

    [Fact]
    public void Victory_AppliesLossesRewardsAndCompletionExactlyOnce()
    {
        var campaign = Create();
        var session = Start(campaign);
        Damage(Assert.Single(session.Battle.Units, u => u.Side == BattleSide.Player), 13);
        Win(session);
        var result = campaign.ResolveEncounter(session);
        Assert.True(result.Success);
        Assert.True(result.Victory);
        Assert.Equal(20, result.GoldAwarded);
        Assert.Equal(30, result.ExperienceAwarded);
        Assert.Equal(116, campaign.Hero.Gold);
        Assert.Equal(30, campaign.Hero.Experience);
        Assert.Equal(3, Assert.Single(campaign.Hero.Army.Stacks).Count);
        Assert.Contains("road", campaign.CompletedEncounterIds);
        Assert.False(campaign.ResolveEncounter(session).Success);
        Assert.False(campaign.TryStartEncounter(Point, Point.Coordinate, Catalog()).Success);
        Assert.Equal(116, campaign.Hero.Gold);
    }

    [Fact]
    public void Defeat_AppliesLossesWithoutRewardsAndCanBeRetriedWithRecruits()
    {
        var campaign = Create();
        var session = Start(campaign);
        Damage(Assert.Single(session.Battle.Units, u => u.Side == BattleSide.Player), long.MaxValue);
        var result = campaign.ResolveEncounter(session);
        Assert.True(result.Success);
        Assert.False(result.Victory);
        Assert.Empty(campaign.Hero.Army.Stacks);
        Assert.Empty(campaign.CompletedEncounterIds);
        Assert.Equal(96, campaign.Hero.Gold);
        Assert.Equal(0, campaign.Hero.Experience);
        Recruit(campaign.Hero, "flora.sprout", 1);
        Assert.True(campaign.TryStartEncounter(Point, Point.Coordinate, Catalog()).Success);
    }

    [Fact]
    public void Resolve_RejectsUnfinishedForeignAndArmyChangedSessionsWithoutMutation()
    {
        var campaign = Create();
        var session = Start(campaign);
        Assert.False(campaign.ResolveEncounter(session).Success);
        var foreign = Start(Create());
        Win(foreign);
        Assert.False(campaign.ResolveEncounter(foreign).Success);
        Recruit(campaign.Hero, "flora.thorn-guard", 1);
        Win(session);
        Assert.False(campaign.ResolveEncounter(session).Success);
        Assert.Equal(5, Assert.Single(campaign.Hero.Army.Stacks).Count);
        Assert.Empty(campaign.CompletedEncounterIds);
        Assert.Equal(0, campaign.Hero.Experience);
    }

    [Theory]
    [InlineData(int.MaxValue, 30)]
    [InlineData(20, int.MaxValue)]
    public void Resolve_RewardOverflowDoesNotPartiallyApplyLossesOrCompletion(int gold, int experience)
    {
        var campaign = Create(gold: gold, xp: experience);
        campaign.Hero.GainExperience(1);
        var session = Start(campaign);
        Damage(Assert.Single(session.Battle.Units, u => u.Side == BattleSide.Player), 13);
        Win(session);
        Assert.False(campaign.ResolveEncounter(session).Success);
        Assert.Equal(4, Assert.Single(campaign.Hero.Army.Stacks).Count);
        Assert.Equal(96, campaign.Hero.Gold);
        Assert.Equal(1, campaign.Hero.Experience);
        Assert.Empty(campaign.CompletedEncounterIds);
    }

    [Fact]
    public void ConvertedEnemy_IsExcludedAndOriginalSurvivorsStartNextBattleFullyHealed()
    {
        var hero = new HeroModel("hero", "Hero", 100);
        Recruit(hero, "flora.thorn-guard", 4);
        var secondPoint = new WorldPointOfInterest("second", "battle", "Second", new(3, 3));
        var campaign = new Campaign(hero, [], encounters: [Encounter(),
            new("second", secondPoint.Id, "Second", 456, [new("human.militia", 1)], 0, 0)]);
        var session = Start(campaign);
        Damage(Assert.Single(session.Battle.Units, u => u.Side == BattleSide.Player), 13);
        Convert(Assert.Single(session.Battle.Units, u => u.Side == BattleSide.Enemy), BattleSide.Player);
        Assert.True(campaign.ResolveEncounter(session).Success);
        var survivor = Assert.Single(campaign.Hero.Army.Stacks);
        Assert.Equal("flora.thorn-guard", survivor.UnitTypeId);
        Assert.Equal(3, survivor.Count);
        var next = campaign.TryStartEncounter(secondPoint, secondPoint.Coordinate, Catalog()).Session!;
        Assert.Equal(30, Assert.Single(next.Battle.Units, u => u.Side == BattleSide.Player).RemainingHitPoints);
    }

    [Fact]
    public void Start_RejectsUnknownTypesAndStackHitPointOverflow()
    {
        var hero = new HeroModel("hero", "Hero", int.MaxValue);
        Recruit(hero, "unknown", 1);
        Assert.False(new Campaign(hero, [], encounters: [Encounter()])
            .TryStartEncounter(Point, Point.Coordinate, Catalog()).Success);
        var huge = new HeroModel("huge", "Huge", int.MaxValue);
        Recruit(huge, "flora.thorn-guard", int.MaxValue);
        Assert.False(new Campaign(huge, [], encounters: [Encounter()])
            .TryStartEncounter(Point, Point.Coordinate, Catalog()).Success);
    }
}

