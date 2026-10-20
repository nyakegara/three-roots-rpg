using Game.Core.Combat;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat.Rules;

public sealed class BattleRulesTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CanAttack_ObstacleUsesExplicitSightFlag(bool blocksSight, bool expected)
    {
        var attacker = Unit("archer", BattleSide.Player, new HexCoord(0, 0), range: 3);
        var target = Unit("target", BattleSide.Enemy, new HexCoord(2, 0));
        var map = new BattleMap([new BattleTile(new(0, 0)),
            new BattleTile(new(1, 0), isPassable: false, blocksLineOfSight: blocksSight), new BattleTile(new(2, 0))]);
        Assert.Equal(expected, BattleRules.CanAttack(attacker, target, map));
        Assert.False(map.GetTile(new HexCoord(1, 0))!.IsPassable);
    }

    [Fact]
    public void GetEffectiveRange_HigherAttacker_AddsTwoCells()
    {
        var attacker = Unit("archer", BattleSide.Player, new HexCoord(0, 0), height: 2, range: 3);
        var target = Unit("target", BattleSide.Enemy, new HexCoord(5, 0));
        var map = new BattleMap(Enumerable.Range(0, 6).Select(q =>
            new BattleTile(new HexCoord(q, 0), q == 0 ? 2 : 0)));

        Assert.Equal(5, BattleRules.GetEffectiveRange(attacker, map.GetTile(target.Position)!));
        Assert.True(BattleRules.CanAttack(attacker, target, map));
    }

    [Fact]
    public void CanAttack_WallBetweenUnits_BlocksLineOfSight()
    {
        var attacker = Unit("archer", BattleSide.Player, new HexCoord(0, 0), range: 4);
        var target = Unit("target", BattleSide.Enemy, new HexCoord(3, 0));
        var map = new BattleMap(new[] { new BattleTile(new(0, 0)), new BattleTile(new(1, 0)),
            new BattleTile(new(2, 0), blocksLineOfSight: true), new BattleTile(new(3, 0)) });

        Assert.False(BattleRules.CanAttack(attacker, target, map));
    }

    [Fact]
    public void CanAttack_CliffBetweenUnits_BlocksLineOfSight()
    {
        var attacker = Unit("archer", BattleSide.Player, new HexCoord(0, 0), range: 4);
        var target = Unit("target", BattleSide.Enemy, new HexCoord(3, 0));
        var map = new BattleMap(new[] { new BattleTile(new(0, 0)), new BattleTile(new(1, 0), height: 3),
            new BattleTile(new(2, 0)), new BattleTile(new(3, 0)) });

        Assert.False(BattleRules.CanAttack(attacker, target, map));
    }

    [Fact]
    public void CanAttack_CliffBelowHigherTargetButAboveShotLine_BlocksLineOfSight()
    {
        var attacker = Unit("archer", BattleSide.Player, new HexCoord(0, 0), range: 4);
        var target = Unit("target", BattleSide.Enemy, new HexCoord(3, 0), height: 4);
        var map = new BattleMap(new[] { new BattleTile(new(0, 0)), new BattleTile(new(1, 0), height: 3),
            new BattleTile(new(2, 0)), new BattleTile(new(3, 0), height: 4) });

        Assert.False(BattleRules.CanAttack(attacker, target, map));
    }

    [Fact]
    public void CanAttack_ElevationBonus_DoesNotIgnoreWall()
    {
        var attacker = Unit("archer", BattleSide.Player, new HexCoord(0, 0), height: 2, range: 1);
        var target = Unit("target", BattleSide.Enemy, new HexCoord(3, 0));
        var map = new BattleMap(new[] { new BattleTile(new(0, 0), height: 2),
            new BattleTile(new(1, 0), isPassable: false), new BattleTile(new(2, 0)), new BattleTile(new(3, 0)) });

        Assert.False(BattleRules.CanAttack(attacker, target, map));
    }

    [Fact]
    public void CanAttack_UnitOnImpassableCell_IsRejected()
    {
        var attacker = Unit("archer", BattleSide.Player, new HexCoord(0, 0), range: 4);
        var target = Unit("target", BattleSide.Enemy, new HexCoord(2, 0));
        var map = new BattleMap(new[] { new BattleTile(new(0, 0)), new BattleTile(new(1, 0)),
            new BattleTile(new(2, 0), isPassable: false) });

        Assert.False(BattleRules.CanAttack(attacker, target, map));
    }

    [Fact]
    public void CanAttackTile_PreviewMatchesRealTargetRules()
    {
        var attacker = Unit("archer", BattleSide.Player, new HexCoord(0, 0), range: 6);
        var clear = Unit("clear", BattleSide.Enemy, new HexCoord(4, 0));
        var blocked = Unit("blocked", BattleSide.Enemy, new HexCoord(0, 3));
        var map = new BattleMap(Enumerable.Range(0, 7).SelectMany(q =>
            Enumerable.Range(0, 4).Select(r => new BattleTile(new HexCoord(q, r),
                blocksLineOfSight: q == 0 && r == 2))));

        Assert.True(BattleRules.CanAttackTile(attacker, map.GetTile(clear.Position)!, map));
        Assert.True(BattleRules.CanAttack(attacker, clear, map));
        Assert.False(BattleRules.CanAttackTile(attacker, map.GetTile(blocked.Position)!, map));
        Assert.False(BattleRules.CanAttack(attacker, blocked, map));
        Assert.False(BattleRules.CanAttackTile(attacker, map.GetTile(new HexCoord(0, 0))!, map));
    }

    private static BattleUnit Unit(string id, BattleSide side, HexCoord position,
        int height = 0, int range = 1) =>
        new(id, side, position, count: 3, hitPointsPerCreature: 10, damagePerCreature: 2,
            speed: 4, movement: 3, range: range, isRanged: range > 1, height: height);
}
