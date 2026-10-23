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

    [Theory]
    [InlineData(true, 8)]
    [InlineData(false, 10)]
    public void KnightReducesRangedDamageOnly(bool ranged, int expectedDamage)
    {
        var attacker = Unit("attacker", BattleSide.Player, 0, 0, 10, ranged: ranged);
        var knight = Unit("knight", BattleSide.Enemy, 1, 0, 1, UnitAbility.KnightWard);
        var battle = BattleWith(attacker, knight);
        battle.StartRound();
        Assert.Equal(expectedDamage, BattleRules.GetAttackDamage(attacker, knight));
        Assert.True(battle.Attack(attacker.Id, knight.Id));
        Assert.Equal(30 - expectedDamage, knight.RemainingHitPoints);
    }

    [Fact]
    public void KnightWardHandlesLargeRangedStacksWithoutOverflow()
    {
        var attacker = new BattleUnit("shooter", BattleSide.Player, new(0, 0), int.MaxValue,
            1, int.MaxValue, 7, 0, 7, true);
        var knight = Unit("knight", BattleSide.Enemy, 1, 0, 1, UnitAbility.KnightWard);
        Assert.Equal(checked((long)int.MaxValue * int.MaxValue) / 5 * 4 +
            checked((long)int.MaxValue * int.MaxValue) % 5 * 4 / 5,
            BattleRules.GetAttackDamage(attacker, knight));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 20)]
    public void HunterHasTenPercentChanceToDodgeAnyHit(int roll, int remainingHitPoints)
    {
        var attacker = Unit("attacker", BattleSide.Player, 0, 0, 10, ranged: true);
        var hunter = Unit("hunter", BattleSide.Enemy, 1, 0, 1, UnitAbility.Hunter);
        var battle = new Battle(BattleWith(attacker, hunter).Map, [attacker, hunter], new FixedRandom(roll));
        battle.StartRound();
        Assert.True(battle.Attack(attacker.Id, hunter.Id));
        Assert.Equal(remainingHitPoints, hunter.RemainingHitPoints);
    }

    private sealed class FixedRandom(int value) : Random
    {
        public override int Next(int maxValue) => value;
    }

    [Fact]
    public void InquisitorHammerAndFireHaveDifferentDamage()
    {
        var inquisitor = new BattleUnit("inquisitor", BattleSide.Player, new(0, 0), 2,
            40, 12, 7, 3, 1, false, ability: UnitAbility.RadiantModes);
        var target = Unit("target", BattleSide.Enemy, 1, 0, 1);
        var battle = BattleWith(inquisitor, target);
        battle.StartRound();
        Assert.Equal(24, BattleRules.GetAttackDamage(inquisitor, target));
        Assert.True(battle.TrySetAttackMode(inquisitor.Id, AttackMode.HolyFire));
        Assert.Equal(16, BattleRules.GetAttackDamage(inquisitor, target));
        Assert.True(battle.Attack(inquisitor.Id, target.Id));
        Assert.Equal(14, target.RemainingHitPoints);
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
        Assert.Equal(6, BattleRules.GetEffectiveRange(inquisitor, battle.Map.GetTile(distant.Position)!));
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
        Assert.False(field.Map.GetTile(At(6, 3))!.IsPassable);
        Assert.False(field.Map.GetTile(At(6, 4))!.IsPassable);
        Assert.True(field.Map.GetTile(At(6, 6))!.IsPassable);
        Assert.False(field.Map.GetTile(At(6, 2))!.BlocksLineOfSight);
        var path = new Game.Core.Combat.Pathfinding.AStarPathfinder().FindPath(field.Map, At(3, 2), At(9, 2));
        Assert.True(path.IsReachable);
        Assert.InRange(path.Cost, 9, 14);
        Assert.Contains(path.Coordinates, cell => cell.R >= 5);
        Assert.Equal(2, field.Map.GetTile(At(9, 2))!.Height);
        Assert.Equal(2, field.Map.GetTile(At(9, 5))!.Height);
        Assert.True(field.Map.GetTile(field.PlayerStart)!.IsPassable);
        Assert.True(field.Map.GetTile(field.EnemyStart)!.IsPassable);
    }

    [Fact]
    public void OutpostCliffAllowsReturnFireAndWideAscentAroundDefenders()
    {
        var field = MountainOutpostBattlefield.Create(42);
        var low = new BattleUnit("low", BattleSide.Player, At(5, 2), 1, 10, 3, 4, 4, 7, true);
        var high = new BattleUnit("high", BattleSide.Enemy, At(9, 2), 1, 10, 3, 4, 4, 7, true);
        _ = new Battle(field.Map, [low, high]);
        Assert.True(BattleRules.CanAttackTile(low, field.Map.GetTile(high.Position)!, field.Map));
        Assert.True(BattleRules.CanAttackTile(high, field.Map.GetTile(low.Position)!, field.Map));
        var occupied = new HashSet<HexCoord> { At(5, 5), At(7, 6) };
        var path = new Game.Core.Combat.Pathfinding.AStarPathfinder().FindPath(field.Map, At(3, 2), At(9, 1), occupied);
        Assert.True(path.IsReachable);
        Assert.Contains(path.Coordinates, cell => cell.R >= 5);
        Assert.InRange(path.Cost, 9, 14);
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
