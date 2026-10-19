using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Combat.Generation;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat;

public sealed class HumanUnitMechanicsTests
{
    [Theory]
    [InlineData(true, 9)]
    [InlineData(false, 6)]
    public void HunterDealsBonusOnlyToRangedStacks(bool rangedTarget, int expectedDamage)
    {
        var hunter = Unit("hunter", BattleSide.Player, 0, 0, 6, UnitAbility.Hunter);
        var target = Unit("target", BattleSide.Enemy, 1, 0, 1, ranged: rangedTarget);
        var battle = BattleWith(hunter, target);
        battle.StartRound();

        Assert.True(battle.Attack(hunter.Id, target.Id));
        Assert.Equal(30 - expectedDamage, target.RemainingHitPoints);
    }

    [Fact]
    public void InquisitorMayChooseFireForDistanceOrHammerForAdjacentTarget()
    {
        var inquisitor = Unit("inquisitor", BattleSide.Player, 0, 0, 8, UnitAbility.RadiantModes);
        var distant = Unit("target", BattleSide.Enemy, 3, 0, 1);
        var battle = BattleWith(inquisitor, distant);
        battle.StartRound();

        Assert.False(BattleRules.CanAttack(inquisitor, distant, battle.Map));
        Assert.True(battle.TrySetAttackMode(inquisitor.Id, AttackMode.HolyFire));
        Assert.True(BattleRules.CanAttack(inquisitor, distant, battle.Map));
        Assert.True(battle.Attack(inquisitor.Id, distant.Id));
        Assert.False(battle.TrySetAttackMode(inquisitor.Id, AttackMode.Hammer));
    }

    [Fact]
    public void EnemyInquisitorSelectsFireThenShootsAtDistantTarget()
    {
        var inquisitor = Unit("inquisitor", BattleSide.Enemy, 0, 0, 8, UnitAbility.RadiantModes);
        var target = Unit("target", BattleSide.Player, 3, 0, 1);
        var interaction = new BattleInteraction(BattleWith(inquisitor, target));

        var modeAction = interaction.StepOpponent();
        Assert.Equal(BattleActionType.SetAttackMode, modeAction!.Type);
        Assert.Equal(AttackMode.HolyFire, inquisitor.AttackMode);
        var attackAction = interaction.StepOpponent();
        Assert.Equal(BattleActionType.Attack, attackAction!.Type);
        Assert.True(target.RemainingHitPoints < 30);
    }

    [Fact]
    public void MountainOutpostHasPassableBreachAndElevatedArcherPosts()
    {
        var field = MountainOutpostBattlefield.Create(42);
        Assert.False(field.Map.GetTile(At(6, 2))!.IsPassable);
        Assert.True(field.Map.GetTile(At(6, 3))!.IsPassable);
        Assert.True(field.Map.GetTile(At(6, 4))!.IsPassable);
        Assert.Equal(2, field.Map.GetTile(At(9, 2))!.Height);
        Assert.Equal(2, field.Map.GetTile(At(9, 5))!.Height);
        Assert.True(field.Map.GetTile(field.PlayerStart)!.IsPassable);
        Assert.True(field.Map.GetTile(field.EnemyStart)!.IsPassable);
    }

    private static Battle BattleWith(params BattleUnit[] units) =>
        new(new BattleMap(from q in Enumerable.Range(0, 7)
            from r in Enumerable.Range(0, 2)
            select new BattleTile(new HexCoord(q, r))), units);

    private static BattleUnit Unit(string id, BattleSide side, int q, int r, int damage,
        UnitAbility ability = UnitAbility.None, bool ranged = false) =>
        new(id, side, new HexCoord(q, r), 1, 30, damage, 7, 4, ranged ? 4 : 1,
            ranged, ability: ability);

    private static HexCoord At(int column, int row) => new(column - row / 2, row);
}
