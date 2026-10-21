using Game.Core.Combat;
using Game.Core.Geometry;
using Game.MonoGame.Rendering;
using Xunit;

namespace Game.Tests;

public sealed class BattleHudCommandTests
{
    [Fact]
    public void PlayerCommands_AreUnavailableDuringActionAndReturnAfterIt()
    {
        var battle = Create();
        Assert.False(BattleHud.CanIssueCommands(battle, false));
        battle.StartRound();
        Assert.True(BattleHud.CanIssueCommands(battle, false));
        Assert.False(BattleHud.CanIssueCommands(battle, true));
        Assert.True(BattleHud.CanIssueCommands(battle, false));
        Assert.False(battle.HasMoved("player"));
        Assert.False(battle.HasAttacked("player"));
    }

    [Fact]
    public void EnemyTurnAndFinishedBattle_DoNotEnablePlayerCommands()
    {
        var battle = Create();
        battle.StartRound();
        battle.EndActivation();
        Assert.False(BattleHud.CanIssueCommands(battle, false));
        battle.EndActivation();
        battle.StartRound();
        Assert.True(battle.Attack("player", "enemy"));
        Assert.True(battle.IsFinished);
        Assert.False(BattleHud.CanIssueCommands(battle, false));
    }

    [Fact]
    public void NonlethalAttack_DoesNotDisableRemainingMovementAfterAnimation()
    {
        var battle = Create(enemyCount: 2);
        battle.StartRound();
        Assert.True(battle.Attack("player", "enemy"));
        Assert.False(BattleHud.CanIssueCommands(battle, true));
        Assert.True(BattleHud.CanIssueCommands(battle, false));
        Assert.True(battle.Move("player", new(0, 1)));
    }

    private static Battle Create(int enemyCount = 1) => new(new BattleMap([
        new BattleTile(new(0, 0)), new BattleTile(new(1, 0)), new BattleTile(new(0, 1))]), [
        new BattleUnit("player", BattleSide.Player, new(0, 0), 1, 10, 10, 8, 2, 1, false),
        new BattleUnit("enemy", BattleSide.Enemy, new(1, 0), enemyCount, 10, 2, 1, 2, 1, false)]);
}
