using Game.Core.Combat;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat;

public sealed class AttackDamagePreviewTests
{
    [Theory]
    [InlineData(UnitAbility.None, false, UnitAbility.None, AttackMode.Hammer, 15)]
    [InlineData(UnitAbility.Hunter, false, UnitAbility.None, AttackMode.Hammer, 15)]
    [InlineData(UnitAbility.Hunter, true, UnitAbility.None, AttackMode.Hammer, 22)]
    [InlineData(UnitAbility.Hunter, false, UnitAbility.RadiantModes, AttackMode.Hammer, 15)]
    [InlineData(UnitAbility.Hunter, false, UnitAbility.RadiantModes, AttackMode.HolyFire, 22)]
    public void PredictedDamage_EqualsActualHealthLostBySurvivingTarget(UnitAbility ability,
        bool rangedTarget, UnitAbility targetAbility, AttackMode targetMode, long expected)
    {
        var attacker = new BattleUnit("attacker", BattleSide.Player, new HexCoord(0, 0),
            3, 10, 5, 1, 2, 1, false, ability: ability);
        var target = new BattleUnit("target", BattleSide.Enemy, new HexCoord(1, 0),
            1, 100, 1, 2, 2, 7, rangedTarget, ability: targetAbility);
        var battle = CreateBattle(attacker, target);
        battle.StartRound();
        if (targetMode == AttackMode.HolyFire)
            Assert.True(battle.TrySetAttackMode(target.Id, targetMode));
        battle.EndActivation();

        var predicted = BattleRules.GetAttackDamage(attacker, target);
        var before = target.RemainingHitPoints;
        Assert.True(battle.Attack(attacker.Id, target.Id));

        Assert.Equal(expected, predicted);
        Assert.True(target.IsAlive);
        Assert.Equal(predicted, before - target.RemainingHitPoints);
    }

    [Fact]
    public void HunterBonus_PreservesCheckedOverflowForPreviewAndAttack()
    {
        var attacker = new BattleUnit("attacker", BattleSide.Player, new HexCoord(0, 0),
            int.MaxValue, 1, int.MaxValue, 2, 2, 1, false, ability: UnitAbility.Hunter);
        var target = new BattleUnit("target", BattleSide.Enemy, new HexCoord(1, 0),
            1, 100, 1, 1, 2, 7, true);
        var battle = CreateBattle(attacker, target);
        battle.StartRound();

        Assert.Throws<OverflowException>(() => BattleRules.GetAttackDamage(attacker, target));
        Assert.Throws<OverflowException>(() => battle.Attack(attacker.Id, target.Id));
        Assert.Equal(100, target.RemainingHitPoints);
        Assert.False(battle.HasAttacked(attacker.Id));
    }

    private static Battle CreateBattle(BattleUnit attacker, BattleUnit target) =>
        new(new BattleMap([new BattleTile(attacker.Position), new BattleTile(target.Position)]),
            [attacker, target], new Random(1));
}
