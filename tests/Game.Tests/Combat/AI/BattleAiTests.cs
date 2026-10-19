using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat.AI;

public sealed class BattleAiTests
{
    [Fact]
    public void ChooseAction_WhenNoTargetInRange_MovesTowardReachableEnemy()
    {
        var ai = new BattleAi();
        var enemy = new BattleUnit("ai-archer", BattleSide.Enemy, new(0, 0), 3, 10, 3,
            speed: 6, movement: 2, range: 2, isRanged: true);
        var player = new BattleUnit("player", BattleSide.Player, new(5, 0), 3, 10, 2,
            speed: 1, movement: 2, range: 1, isRanged: false);
        var tiles = from q in Enumerable.Range(0, 7)
                    from r in Enumerable.Range(0, 4)
                    select new BattleTile(new HexCoord(q, r));
        var battle = new Battle(new BattleMap(tiles), new[] { enemy, player });
        battle.StartRound();

        var action = ai.ChooseAction(battle, "ai-archer");

        Assert.Equal(BattleActionType.Move, action.Type);
        Assert.NotNull(action.Destination);
        Assert.True(action.Destination!.Value.DistanceTo(enemy.Position) < 5);
    }

    [Fact]
    public void ChooseAction_WhenTargetIsAttackable_ReturnsAttack()
    {
        var ai = new BattleAi();
        var enemy = new BattleUnit("ai-archer", BattleSide.Enemy, new(0, 0), 3, 10, 3,
            speed: 6, movement: 2, range: 3, isRanged: true);
        var player = new BattleUnit("player", BattleSide.Player, new(2, 0), 3, 10, 2,
            speed: 1, movement: 2, range: 1, isRanged: false);
        var map = new BattleMap(new[] { new BattleTile(enemy.Position), new BattleTile(new(1, 0)),
            new BattleTile(player.Position) });
        var battle = new Battle(map, new[] { enemy, player });
        battle.StartRound();

        var action = ai.ChooseAction(battle, "ai-archer");

        Assert.Equal(BattleActionType.Attack, action.Type);
        Assert.Equal("player", action.TargetId);
    }
}
