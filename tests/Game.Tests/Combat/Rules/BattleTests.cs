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

    [Fact]
    public void Heal_RestoresTenHitPointsAndResurrectsCreatureAtThreshold()
    {
        var attacker = Unit("attacker", BattleSide.Enemy, new(3, 0), speed: 10, range: 3,
            damage: 10, count: 1);
        var healer = Unit("healer", BattleSide.Player, new(0, 0), speed: 8,
            ability: UnitAbility.HealAlly);
        var ally = Unit("ally", BattleSide.Player, new(2, 0), count: 10, hp: 10);
        var battle = CreateBattle(attacker, healer, ally);
        battle.StartRound();
        Assert.True(battle.Attack("attacker", "ally"));
        battle.EndActivation();
        Assert.Equal(90, ally.RemainingHitPoints);
        Assert.Equal(9, ally.Count);

        Assert.True(battle.TryHeal("healer", "ally"));

        Assert.Equal(100, ally.MaximumHitPointsAtBattleStart);
        Assert.Equal(100, ally.RemainingHitPoints);
        Assert.Equal(10, ally.Count);
    }

    [Fact]
    public void BattleUnit_OneHitPointOfHealingAtNinetyResurrectsTenthCreature()
    {
        var unit = Unit("ally", BattleSide.Player, new(0, 0), count: 10, hp: 10);
        typeof(BattleUnit).GetMethod("ApplyDamage", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.Invoke(unit, [10L]);

        var healed = (int)typeof(BattleUnit).GetMethod("Heal", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.Invoke(unit, [1])!;

        Assert.Equal(1, healed);
        Assert.Equal(91, unit.RemainingHitPoints);
        Assert.Equal(10, unit.Count);
    }

    [Fact]
    public void Heal_CapsAtBattleStartMaximumAndReturnsOnlyEffectiveHealing()
    {
        var unit = Unit("ally", BattleSide.Player, new(0, 0), count: 10, hp: 10);
        var damage = typeof(BattleUnit).GetMethod("ApplyDamage", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!;
        var heal = typeof(BattleUnit).GetMethod("Heal", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!;
        damage.Invoke(unit, [3L]);

        var healed = (int)heal.Invoke(unit, [10])!;

        Assert.Equal(3, healed);
        Assert.Equal(100, unit.RemainingHitPoints);
        Assert.Equal(0, (int)heal.Invoke(unit, [int.MaxValue])!);

        var large = Unit("large", BattleSide.Player, new(1, 0), count: 1, hp: int.MaxValue);
        damage.Invoke(large, [1L]);
        Assert.Equal(1, (int)heal.Invoke(large, [int.MaxValue])!);
        Assert.Equal(int.MaxValue, large.RemainingHitPoints);
    }

    [Fact]
    public void Heal_RejectsDeadFullEnemySelfAndNonHealerTargets()
    {
        var enemy = Unit("enemy", BattleSide.Enemy, new(3, 0), speed: 11, range: 3,
            damage: 10, count: 1);
        var attacker = Unit("attacker", BattleSide.Player, new(1, 1), speed: 10,
            range: 3, damage: 2, count: 1);
        var executioner = Unit("executioner", BattleSide.Enemy, new(3, 1), speed: 9,
            range: 3, damage: 10, count: 1);
        var healer = Unit("healer", BattleSide.Player, new(0, 0), speed: 8,
            ability: UnitAbility.HealAlly);
        var full = Unit("full", BattleSide.Player, new(1, 0));
        var dead = Unit("dead", BattleSide.Player, new(2, 0), count: 1);
        var battle = CreateBattle(enemy, attacker, executioner, healer, full, dead);
        battle.StartRound();
        Assert.True(battle.Attack("enemy", "healer"));
        battle.EndActivation();
        Assert.False(battle.TryHeal("attacker", "healer"));
        Assert.True(battle.Attack("attacker", "enemy"));
        battle.EndActivation();
        Assert.True(battle.Attack("executioner", "dead"));
        battle.EndActivation();

        Assert.False(battle.TryHeal("healer", "dead"));
        Assert.False(battle.TryHeal("healer", "full"));
        Assert.False(battle.TryHeal("healer", "enemy"));
        Assert.False(battle.TryHeal("healer", "healer"));
        Assert.False(battle.HasAttacked("healer"));
    }

    [Fact]
    public void Heal_ConsumesAttackActionButAllowsMovementFirst()
    {
        var enemy = Unit("enemy", BattleSide.Enemy, new(4, 0), speed: 10, range: 3,
            damage: 2, count: 1);
        var healer = Unit("healer", BattleSide.Player, new(0, 0), speed: 8, range: 3,
            ability: UnitAbility.HealAlly);
        var ally = Unit("ally", BattleSide.Player, new(2, 0));
        var battle = CreateBattle(enemy, healer, ally);
        battle.StartRound();
        Assert.True(battle.Attack("enemy", "ally"));
        battle.EndActivation();

        Assert.True(battle.Move("healer", new(0, 1)));
        Assert.True(battle.TryHeal("healer", "ally"));
        Assert.True(battle.HasAttacked("healer"));
        Assert.False(battle.Attack("healer", "enemy"));
        Assert.False(battle.TryHeal("healer", "ally"));
    }

    [Fact]
    public void MushroomAttack_WhenOneInEightRollSucceeds_ConvertsLivingStackAndKeepsItsInitiativeSlot()
    {
        var random = new SequenceRandom(0);
        var mushroom = Unit("mushroom", BattleSide.Player, new(0, 0), speed: 10, range: 3,
            damage: 2, count: 1, ability: UnitAbility.ConvertOnHit);
        var target = Unit("target", BattleSide.Enemy, new(2, 0), speed: 8, count: 3, range: 3);
        var remainingEnemy = Unit("remaining-enemy", BattleSide.Enemy, new(4, 0), speed: 6);
        var battle = CreateBattle(random, mushroom, target, remainingEnemy);
        battle.StartRound();

        Assert.True(battle.Attack("mushroom", "target"));

        Assert.Equal(BattleSide.Player, target.Side);
        Assert.Equal(BattleSide.Enemy, target.OriginalSide);
        Assert.Equal(1, random.Calls);
        Assert.Equal(8, random.LastMaxValue);
        Assert.Equal(3, target.Count);
        Assert.Equal(28, target.RemainingHitPoints);
        Assert.DoesNotContain(target, battle.GetEnemies(BattleSide.Player));
        Assert.Contains(remainingEnemy, battle.GetEnemies(BattleSide.Player));
        Assert.Equal("mushroom", battle.ActiveUnit!.Id);

        battle.EndActivation();

        Assert.Equal("target", battle.ActiveUnit!.Id);
        Assert.True(battle.Attack("target", "remaining-enemy"));
        Assert.Equal(24, remainingEnemy.RemainingHitPoints);
    }

    [Fact]
    public void MushroomAttack_WhenOneInEightRollFails_DoesNotConvertTarget()
    {
        var mushroom = Unit("mushroom", BattleSide.Player, new(0, 0), speed: 10, range: 3,
            ability: UnitAbility.ConvertOnHit);
        var target = Unit("target", BattleSide.Enemy, new(2, 0));
        var battle = CreateBattle(new SequenceRandom(1), mushroom, target);
        battle.StartRound();

        Assert.True(battle.Attack("mushroom", "target"));

        Assert.Equal(BattleSide.Enemy, target.Side);
    }

    [Fact]
    public void MushroomAttack_WhenTargetDies_DoesNotConvertDeadStackOrRollForConversion()
    {
        var random = new SequenceRandom(0);
        var mushroom = Unit("mushroom", BattleSide.Player, new(0, 0), speed: 10, range: 3,
            damage: 20, ability: UnitAbility.ConvertOnHit);
        var target = Unit("target", BattleSide.Enemy, new(2, 0), count: 1);
        var battle = CreateBattle(random, mushroom, target);
        battle.StartRound();

        Assert.True(battle.Attack("mushroom", "target"));

        Assert.False(target.IsAlive);
        Assert.Equal(BattleSide.Enemy, target.Side);
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void MushroomAttack_ConvertingLastEnemyFinishesBattleForAttackersSide()
    {
        var mushroom = Unit("mushroom", BattleSide.Player, new(0, 0), speed: 10, range: 3,
            ability: UnitAbility.ConvertOnHit);
        var target = Unit("target", BattleSide.Enemy, new(2, 0));
        var battle = CreateBattle(new SequenceRandom(0), mushroom, target);
        battle.StartRound();

        Assert.True(battle.Attack("mushroom", "target"));

        Assert.True(battle.IsFinished);
        Assert.Equal(BattleSide.Player, battle.Winner);
        Assert.Equal(BattleSide.Enemy, target.OriginalSide);
        Assert.Null(battle.ActiveUnit);
        Assert.Single(battle.GetOutcome().SurvivingPlayerStacks);
    }

    [Fact]
    public void GetOutcome_RequiresFinishedBattleAndContainsOnlySurvivingOriginalPlayerStacks()
    {
        var killer = Unit("killer", BattleSide.Enemy, new(0, 0), speed: 12, range: 3,
            damage: 10, count: 1);
        var executioner = Unit("executioner", BattleSide.Enemy, new(6, 0), speed: 13, range: 3,
            damage: 10, count: 1);
        var survivor = Unit("survivor", BattleSide.Player, new(1, 0), speed: 11, range: 5,
            damage: 10, count: 2, unitTypeId: "flora.kabachok");
        var mushroom = Unit("mushroom", BattleSide.Player, new(2, 0), speed: 10, range: 3,
            damage: 2, ability: UnitAbility.ConvertOnHit, unitTypeId: "flora.mushroom");
        var converted = Unit("converted", BattleSide.Enemy, new(3, 0), speed: 9, range: 5,
            damage: 100, count: 4, unitTypeId: "human.guard");
        var finisher = Unit("finisher", BattleSide.Player, new(4, 0), speed: 8,
            range: 5, damage: 10, count: 1, unitTypeId: "flora.orchid");
        var lastEnemy = Unit("last-enemy", BattleSide.Enemy, new(5, 1), speed: 7,
            count: 1, unitTypeId: "human.archer");
        var doomed = Unit("doomed", BattleSide.Player, new(5, 0), speed: 6,
            count: 1, unitTypeId: "flora.dead");
        var battle = CreateBattle(new SequenceRandom(0), killer, executioner, survivor, mushroom,
            converted, finisher, lastEnemy, doomed);

        Assert.Throws<InvalidOperationException>(() => battle.GetOutcome());
        battle.StartRound();
        Assert.True(battle.Attack("executioner", "doomed"));
        battle.EndActivation();
        Assert.True(battle.Attack("killer", "survivor"));
        battle.EndActivation();
        Assert.Equal("survivor", battle.ActiveUnit!.Id);
        Assert.True(battle.Attack("survivor", "last-enemy"));
        battle.EndActivation();
        Assert.True(battle.Attack("mushroom", "converted"));
        battle.EndActivation();
        Assert.Equal("converted", battle.ActiveUnit!.Id);
        Assert.True(battle.Attack("converted", "executioner"));
        battle.EndActivation();
        Assert.Equal("finisher", battle.ActiveUnit!.Id);
        Assert.True(battle.Attack("finisher", "killer"));
        Assert.True(battle.IsFinished);

        var outcome = battle.GetOutcome();

        Assert.Equal(new[] { ("flora.kabachok", 1), ("flora.mushroom", 3), ("flora.orchid", 1) },
            outcome.SurvivingPlayerStacks.Select(stack => (stack.UnitTypeId, stack.Count))
                .OrderBy(stack => stack.UnitTypeId, StringComparer.Ordinal));
        Assert.DoesNotContain(outcome.SurvivingPlayerStacks, stack => stack.UnitTypeId.StartsWith("human.", StringComparison.Ordinal));
    }

    [Fact]
    public void EndActivation_SkipsDefeatedInitiativeSlotsWithoutSkippingNextLivingUnit()
    {
        var battle = CreateBattle(
            Unit("attacker", BattleSide.Player, new(0, 0), speed: 10, range: 3, damage: 10, count: 1),
            Unit("defeated", BattleSide.Enemy, new(1, 0), speed: 9, count: 1),
            Unit("next", BattleSide.Player, new(0, 1), speed: 8),
            Unit("enemy", BattleSide.Enemy, new(4, 0), speed: 7));
        battle.StartRound();
        Assert.True(battle.Attack("attacker", "defeated"));

        battle.EndActivation();

        Assert.Equal("next", battle.ActiveUnit!.Id);
        Assert.False(battle.Move("defeated", new(2, 0)));
        battle.StartRound();
        Assert.Equal(1, battle.Round);
        Assert.Equal("next", battle.ActiveUnit!.Id);
        Assert.True(battle.Move("next", new(0, 2)));
    }

    [Fact]
    public void EndActivation_SkipsDeadFinalSlotAndNextRoundResetsActions()
    {
        var battle = CreateBattle(
            Unit("attacker", BattleSide.Player, new(0, 0), speed: 10, range: 3, damage: 10, count: 1),
            Unit("enemy", BattleSide.Enemy, new(4, 0), speed: 9),
            Unit("defeated", BattleSide.Enemy, new(1, 0), speed: 8, count: 1));
        battle.StartRound();
        Assert.True(battle.Attack("attacker", "defeated"));
        battle.EndActivation();
        Assert.Equal("enemy", battle.ActiveUnit!.Id);

        battle.EndActivation();

        Assert.Null(battle.ActiveUnit);
        battle.StartRound();
        Assert.Equal(2, battle.Round);
        Assert.Equal("attacker", battle.ActiveUnit!.Id);
        Assert.False(battle.HasAttacked("attacker"));
        Assert.True(battle.Move("attacker", new(0, 1)));
    }

    [Fact]
    public void FinishedBattle_HasNoActiveUnitAndCannotStartAnotherRound()
    {
        var battle = CreateBattle(
            Unit("attacker", BattleSide.Player, new(0, 0), speed: 10, damage: 10, count: 1),
            Unit("enemy", BattleSide.Enemy, new(1, 0), count: 1));
        battle.StartRound();
        Assert.True(battle.Attack("attacker", "enemy"));

        Assert.Null(battle.ActiveUnit);
        battle.StartRound();
        battle.EndActivation();

        Assert.Equal(1, battle.Round);
        Assert.Null(battle.ActiveUnit);
        Assert.False(battle.Attack("attacker", "enemy"));
    }

    private static Battle CreateBattle(params BattleUnit[] units) => CreateBattle(null, units);

    private static Battle CreateBattle(Random? random, params BattleUnit[] units)
    {
        var positions = units.Select(unit => unit.Position).ToHashSet();
        var maxQ = Math.Max(7, positions.Max(position => position.Q) + 2);
        var maxR = Math.Max(5, positions.Max(position => position.R) + 2);
        var map = new BattleMap(from q in Enumerable.Range(0, maxQ)
                                from r in Enumerable.Range(0, maxR)
                                select new BattleTile(new HexCoord(q, r)));
        return new Battle(map, units, random);
    }

    private static BattleUnit Unit(string id, BattleSide side, HexCoord position, int speed = 4,
        int movement = 3, int range = 1, int damage = 2, int count = 3, int hp = 10, bool isHero = false,
        UnitAbility ability = UnitAbility.None, string? unitTypeId = null) =>
        new(id, side, position, count, hp, damage, speed, movement, range, range > 1,
            isHero: isHero, ability: ability, unitTypeId: unitTypeId ?? id);

    private sealed class SequenceRandom(int value) : Random
    {
        public int Calls { get; private set; }
        public int LastMaxValue { get; private set; }

        public override int Next(int maxValue)
        {
            Calls++;
            LastMaxValue = maxValue;
            return value;
        }
    }
}
