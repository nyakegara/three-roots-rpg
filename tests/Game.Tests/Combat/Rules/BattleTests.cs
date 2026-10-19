using Game.Core.Combat;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat.Rules;

public sealed class BattleTests
{
    [Fact]
    public void StartRound_OrdersBySpeedThenStableId()
    {
        var battle = CreateBattle(Unit("zeta", BattleSide.Player, new(0, 0), speed: 5),
            Unit("beta", BattleSide.Enemy, new(4, 0), speed: 5),
            Unit("swift", BattleSide.Enemy, new(3, 0), speed: 8));

        battle.StartRound();

        Assert.Equal("swift", battle.ActiveUnit!.Id);
        battle.EndActivation();
        Assert.Equal("beta", battle.ActiveUnit!.Id);
        battle.EndActivation();
        Assert.Equal("zeta", battle.ActiveUnit!.Id);
    }

    [Fact]
    public void StartRound_DuringActiveRound_DoesNotResetActivation()
    {
        var battle = CreateBattle(Unit("a", BattleSide.Player, new(0, 0), speed: 8),
            Unit("b", BattleSide.Enemy, new(4, 0)));
        battle.StartRound();
        Assert.True(battle.Move("a", new(1, 0)));

        battle.StartRound();

        Assert.Equal(1, battle.Round);
        Assert.False(battle.Move("a", new(2, 0)));
    }

    [Fact]
    public void Attack_SecondAttackInSameActivation_IsRejected()
    {
        var attacker = Unit("archer", BattleSide.Player, new(0, 0), range: 3, damage: 2);
        var battle = CreateBattle(attacker, Unit("enemy-a", BattleSide.Enemy, new(2, 0)),
            Unit("enemy-b", BattleSide.Enemy, new(0, 2)));
        battle.StartRound();

        Assert.True(battle.Attack("archer", "enemy-a"));
        Assert.False(battle.Attack("archer", "enemy-b"));
    }

    [Fact]
    public void Attack_StackDamage_ReducesCreatureCountAndWoundsLastCreature()
    {
        var attacker = Unit("archer", BattleSide.Player, new(0, 0), range: 3, damage: 6);
        var defender = Unit("enemy", BattleSide.Enemy, new(2, 0), count: 2, hp: 10);
        var battle = CreateBattle(attacker, defender);
        battle.StartRound();

        Assert.True(battle.Attack("archer", "enemy"));

        Assert.Equal(1, defender.Count);
        Assert.Equal(2, defender.RemainingHitPoints);
        Assert.Equal(2, defender.CurrentCreatureHitPoints);
    }

    [Fact]
    public void BattleUnit_Count_DoesNotOverflowAtMaximumHitPoints()
    {
        var unit = new BattleUnit("large", BattleSide.Player, new(0, 0), 1, int.MaxValue, 1,
            speed: 1, movement: 0, range: 1, isRanged: false);

        Assert.Equal(1, unit.Count);
    }

    [Fact]
    public void LethalAttack_FinishesBattleAndRejectsFurtherActions()
    {
        var hero = Unit("hero", BattleSide.Player, new(0, 0), speed: 8, range: 2, damage: 10, count: 1, isHero: true);
        var enemy = Unit("enemy", BattleSide.Enemy, new(1, 0), count: 1, hp: 10);
        var battle = CreateBattle(hero, enemy);
        battle.StartRound();

        Assert.True(battle.Attack("hero", "enemy"));
        Assert.True(battle.IsFinished);
        Assert.Equal(BattleSide.Player, battle.Winner);
        Assert.False(battle.Move("hero", new(0, 1)));
        Assert.False(battle.TryUseHeroAbility("hero"));
    }

    [Fact]
    public void HeroAbility_CanBeUsedOncePerRound_AndResetsNextRound()
    {
        var battle = CreateBattle(Unit("hero", BattleSide.Player, new(0, 0), isHero: true),
            Unit("enemy", BattleSide.Enemy, new(3, 0)));
        battle.StartRound();

        Assert.True(battle.TryUseHeroAbility("hero"));
        Assert.False(battle.TryUseHeroAbility("hero"));
        battle.EndActivation();
        battle.EndActivation();
        battle.StartRound();

        Assert.True(battle.TryUseHeroAbility("hero"));
    }

    [Fact]
    public void Move_RejectsDestinationBeyondMovementAndAllowsOneMovePerActivation()
    {
        var battle = CreateBattle(Unit("runner", BattleSide.Player, new(0, 0), speed: 8, movement: 2),
            Unit("enemy", BattleSide.Enemy, new(5, 0)));
        battle.StartRound();

        Assert.False(battle.Move("runner", new(3, 0)));
        Assert.True(battle.Move("runner", new(2, 0)));
        Assert.False(battle.Move("runner", new(1, 0)));
    }

    private static Battle CreateBattle(params BattleUnit[] units)
    {
        var positions = units.Select(unit => unit.Position).ToHashSet();
        var maxQ = Math.Max(7, positions.Max(position => position.Q) + 2);
        var maxR = Math.Max(5, positions.Max(position => position.R) + 2);
        var map = new BattleMap(from q in Enumerable.Range(0, maxQ)
                                from r in Enumerable.Range(0, maxR)
                                select new BattleTile(new HexCoord(q, r)));
        return new Battle(map, units);
    }

    private static BattleUnit Unit(string id, BattleSide side, HexCoord position, int speed = 4,
        int movement = 3, int range = 1, int damage = 2, int count = 3, int hp = 10, bool isHero = false) =>
        new(id, side, position, count, hp, damage, speed, movement, range, range > 1, isHero: isHero);
}
