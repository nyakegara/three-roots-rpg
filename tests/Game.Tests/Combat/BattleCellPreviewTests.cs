using Game.Core.Combat;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat;

public sealed class BattleCellPreviewTests
{
    [Theory]
    [InlineData(7, 1, false, true)]
    [InlineData(7, 2, false, false)]
    [InlineData(5, 1, true, false)]
    [InlineData(1, 1, false, true)]
    public void ShotCursor_AgreesWithActualCommandAcrossHeightsAndObstacles(
        int distance, int targetHeight, bool blocked, bool canShoot)
    {
        var tiles = Enumerable.Range(0, distance + 1).Select(q => new BattleTile(
            new HexCoord(q, 0), q == distance ? targetHeight : 2, true,
            blocksLineOfSight: blocked && q == 2));
        var shooter = Unit("shooter", BattleSide.Player, new HexCoord(0, 0), ranged: true);
        var target = Unit("enemy", BattleSide.Enemy, new HexCoord(distance, 0));
        var session = new BattleInteraction(new Battle(new BattleMap(tiles), [shooter, target]));

        Assert.Equal(canShoot ? BattleCellAction.RangedAttack : BattleCellAction.Blocked,
            session.GetCellAction(target.Position));
        Assert.Equal(canShoot, session.TryAttack(target.Id));
        Assert.Equal(BattleCellAction.Blocked, session.GetCellAction(target.Position));
    }

    [Fact]
    public void AfterAttack_MoveCursorAndCommandRemainAvailableButSecondAttackDoesNot()
    {
        var actor = Unit("actor", BattleSide.Player, new HexCoord(0, 0));
        var enemy = Unit("enemy", BattleSide.Enemy, new HexCoord(1, 0));
        var destination = new HexCoord(0, 1);
        var session = new BattleInteraction(new Battle(new BattleMap(
            [new BattleTile(actor.Position), new BattleTile(enemy.Position), new BattleTile(destination)]),
            [actor, enemy]));

        Assert.True(session.TryAttack(enemy.Id));
        Assert.Equal(BattleCellAction.Blocked, session.GetCellAction(enemy.Position));
        Assert.Equal(BattleCellAction.Move, session.GetCellAction(destination));
        Assert.False(session.SelectDestination(destination));
        Assert.True(session.SelectDestination(destination));
        Assert.Equal(BattleCellAction.Blocked, session.GetCellAction(new HexCoord(0, 0)));
    }

    [Fact]
    public void HealCursor_RequiresActualHealerAndWoundedOtherAlly()
    {
        var healer = Unit("healer", BattleSide.Player, new HexCoord(0, 0), ability: UnitAbility.HealAlly);
        var ally = Unit("ally", BattleSide.Player, new HexCoord(1, 0));
        var enemy = Unit("enemy", BattleSide.Enemy, new HexCoord(2, 0));
        var session = new BattleInteraction(new Battle(new BattleMap(
            [new BattleTile(healer.Position), new BattleTile(ally.Position), new BattleTile(enemy.Position)]),
            [healer, ally, enemy]));
        Assert.Equal(BattleCellAction.Blocked, session.GetCellAction(ally.Position, healing: true));
        Assert.False(session.TryHeal(ally.Id));
        session.Battle.EndActivation();
        session.Battle.EndActivation();
        Assert.True(session.Battle.Attack(enemy.Id, ally.Id));
        session.Battle.EndActivation();
        session.Battle.StartRound();

        Assert.Equal(BattleCellAction.Blocked, session.GetCellAction(healer.Position, healing: true));
        Assert.Equal(BattleCellAction.Blocked, session.GetCellAction(enemy.Position, healing: true));
        Assert.Equal(BattleCellAction.Heal, session.GetCellAction(ally.Position, healing: true));
        Assert.True(session.TryHeal(ally.Id));
        Assert.Equal(BattleCellAction.Blocked, session.GetCellAction(ally.Position, healing: true));
    }

    private static BattleUnit Unit(string id, BattleSide side, HexCoord position,
        bool ranged = false, UnitAbility ability = UnitAbility.None) =>
        new(id, side, position, 10, 10, 1, id is "shooter" or "actor" or "healer" ? 9 : 2,
            2, 6, ranged, ability: ability);
}
