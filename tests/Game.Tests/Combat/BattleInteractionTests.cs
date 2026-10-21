using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat;

public sealed class BattleInteractionTests
{
    [Fact]
    public void DifferentSecondCellOnlyReplacesPreviewAndSameCellConfirms()
    {
        var session = Create();
        Assert.False(session.SelectDestination(new HexCoord(1, 0)));
        Assert.False(session.SelectDestination(new HexCoord(2, 0)));
        Assert.Equal(new HexCoord(0, 0), session.Battle.ActiveUnit!.Position);
        Assert.Equal(2, session.SelectedPath.Cost);
        Assert.True(session.SelectDestination(new HexCoord(2, 0)));
        Assert.Equal(new HexCoord(2, 0), session.Battle.ActiveUnit!.Position);
        Assert.Null(session.SelectedDestination);
    }

    [Fact]
    public void OccupiedAndOverBudgetCellsNeverMove()
    {
        var session = Create(movement: 1);
        foreach (var target in new[] { new HexCoord(2, 0), new HexCoord(4, 0) })
        {
            Assert.False(session.SelectDestination(target));
            Assert.False(session.SelectDestination(target));
        }
        Assert.Equal(new HexCoord(0, 0), session.Battle.ActiveUnit!.Position);
        Assert.False(session.Battle.HasMoved("player"));
    }

    [Fact]
    public void EndTurnClearsPreviewAndRejectsPlayerCommandsDuringOpponentTurn()
    {
        var session = Create();
        session.SelectDestination(new HexCoord(1, 0));
        Assert.True(session.EndPlayerTurn());
        Assert.Null(session.SelectedDestination);
        Assert.False(session.IsPlayerTurn);
        Assert.False(session.SelectDestination(new HexCoord(3, 0)));
        Assert.False(session.TryAttack("player"));
        Assert.False(session.TryHeal("enemy"));
        Assert.False(session.EndPlayerTurn());
    }

    [Fact]
    public void OpponentExecutesOneActionPerStepAndNextRoundStarts()
    {
        var session = Create();
        session.EndPlayerTurn();
        Assert.Equal(BattleActionType.Move, session.StepOpponent()!.Type);
        Assert.Equal(1, session.Battle.Round);
        Assert.False(session.IsPlayerTurn);
        Assert.Equal(BattleActionType.Attack, session.StepOpponent()!.Type);
        Assert.Equal(BattleActionType.EndActivation, session.StepOpponent()!.Type);
        Assert.True(session.IsPlayerTurn);
        Assert.Equal(2, session.Battle.Round);
        Assert.Null(session.StepOpponent());
    }

    [Fact]
    public void AttackAllowsRemainingMoveButCannotAttackTwice()
    {
        var session = Create(enemyPosition: 1);
        Assert.True(session.TryAttack("enemy"));
        Assert.False(session.TryAttack("enemy"));
        Assert.False(session.SelectDestination(new HexCoord(0, 1)));
        Assert.True(session.SelectDestination(new HexCoord(0, 1)));
    }

    [Fact]
    public void FinishedBattleRejectsAllCommands()
    {
        var player = Unit("player", BattleSide.Player, 0, speed: 9, damage: 100);
        var enemy = Unit("enemy", BattleSide.Enemy, 1, speed: 2);
        var session = new BattleInteraction(new Battle(Map(), [player, enemy]));
        Assert.True(session.TryAttack("enemy"));
        Assert.True(session.Battle.IsFinished);
        Assert.False(session.SelectDestination(new HexCoord(0, 1)));
        Assert.False(session.EndPlayerTurn());
        Assert.Null(session.StepOpponent());
    }

    private static BattleInteraction Create(int movement = 3, int enemyPosition = 4) => new(
        new Battle(Map(), [Unit("player", BattleSide.Player, 0, speed: 9, movement: movement),
            Unit("enemy", BattleSide.Enemy, enemyPosition, speed: 2)]));

    private static BattleMap Map() => new(
        Enumerable.Range(0, 5).SelectMany(q => Enumerable.Range(0, 2)
            .Select(r => new BattleTile(new HexCoord(q, r), 0, true))));

    private static BattleUnit Unit(string id, BattleSide side, int q, int speed, int movement = 3, int damage = 1) =>
        new(id, side, new HexCoord(q, 0), 10, 10, damage, speed, movement, 1, false);
}
