using Game.Core.Combat;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat;

public sealed class UndeadUnitMechanicsTests
{
    [Fact]
    public void SacrificePulse_DamagesEveryEnemyWithinFourCellsAndCostsCurrentHealth()
    {
        var caster = new BattleUnit("nach", BattleSide.Player, new(0, 0), 2, 30, 6, 9, 4, 4, true,
            ability: UnitAbility.SacrificePulse);
        var near = new BattleUnit("near", BattleSide.Enemy, new(1, 0), 3, 10, 2, 4, 3, 1, false);
        var far = new BattleUnit("far", BattleSide.Enemy, new(4, 0), 3, 10, 2, 4, 3, 1, false);
        var outside = new BattleUnit("outside", BattleSide.Enemy, new(5, 0), 3, 10, 2, 4, 3, 1, false);
        var map = new BattleMap(Enumerable.Range(0, 6).Select(q => new BattleTile(new HexCoord(q, 0))));
        var battle = new Battle(map, [caster, near, far, outside]);
        battle.StartRound();

        Assert.True(battle.Attack(caster.Id, near.Id));
        Assert.Equal(18, near.RemainingHitPoints);
        Assert.Equal(18, far.RemainingHitPoints);
        Assert.Equal(30, outside.RemainingHitPoints);
        Assert.Equal(54, caster.RemainingHitPoints);
        Assert.False(battle.Attack(caster.Id, far.Id));
    }

    [Fact]
    public void Utburd_CanMeleeAndHealOnSeparateActivations()
    {
        var utburd = new BattleUnit("utburd", BattleSide.Player, new(0, 0), 2, 8, 3, 5, 6, 1, false,
            ability: UnitAbility.HealAlly);
        var ally = new BattleUnit("ally", BattleSide.Player, new(0, 1), 2, 10, 2, 4, 3, 1, false);
        var enemy = new BattleUnit("enemy", BattleSide.Enemy, new(1, 0), 3, 10, 2, 3, 3, 1, false);
        var battle = new Battle(new BattleMap([new(new(0, 0)), new(new(0, 1)), new(new(1, 0))]),
            [utburd, ally, enemy]);
        battle.StartRound();
        Assert.True(battle.Attack(utburd.Id, enemy.Id));
        Assert.Equal(24, enemy.RemainingHitPoints);
        Assert.False(battle.TryHeal(utburd.Id, ally.Id));
    }
}
