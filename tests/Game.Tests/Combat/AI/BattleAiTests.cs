using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat.AI;

public sealed class BattleAiTests
{
    [Fact]
    public void ChooseAction_UsesReachableEnemyInsteadOfCloserIsolatedEnemy()
    {
        var actor = new BattleUnit("ai", BattleSide.Enemy, new(0, 0), 3, 10, 3,
            speed: 6, movement: 2, range: 1, isRanged: false);
        var isolated = new BattleUnit("isolated", BattleSide.Player, new(2, -1), 3, 10, 2,
            speed: 1, movement: 1, range: 1, isRanged: false);
        var reachable = new BattleUnit("reachable", BattleSide.Player, new(4, 1), 3, 10, 2,
            speed: 1, movement: 1, range: 1, isRanged: false);
        HexCoord[] cells = [new(0, 0), new(0, 1), new(1, 1), new(2, 1), new(3, 1), new(4, 1), new(2, -1)];
        var battle = new Battle(new BattleMap(cells.Select(cell => new BattleTile(cell))), [actor, isolated, reachable]);
        battle.StartRound();
        var action = new BattleAi().ChooseAction(battle, actor.Id);
        Assert.Equal(BattleActionType.Move, action.Type);
        Assert.Equal(new HexCoord(1, 1), action.Destination);
        Assert.True(battle.Move(actor.Id, action.Destination!.Value));
    }

    [Fact]
    public void ChooseAction_DetoursAroundOccupiedCellAndUsesOnlyItsMovementAllowance()
    {
        var actor = new BattleUnit("ai", BattleSide.Enemy, new(0, 0), 3, 10, 3,
            speed: 6, movement: 2, range: 1, isRanged: false);
        var ally = new BattleUnit("ally", BattleSide.Enemy, new(1, 0), 3, 10, 2,
            speed: 1, movement: 1, range: 1, isRanged: false);
        var target = new BattleUnit("player", BattleSide.Player, new(3, 0), 3, 10, 2,
            speed: 1, movement: 1, range: 1, isRanged: false);
        HexCoord[] cells = [new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(0, 1), new(1, 1), new(2, 1)];
        var battle = new Battle(new BattleMap(cells.Select(cell => new BattleTile(cell))), [actor, ally, target]);
        battle.StartRound();
        var action = new BattleAi().ChooseAction(battle, actor.Id);
        Assert.Equal(new HexCoord(1, 1), action.Destination);
        Assert.True(battle.Move(actor.Id, action.Destination!.Value));
        Assert.Equal(BattleActionType.EndActivation, new BattleAi().ChooseAction(battle, actor.Id).Type);
    }

    [Fact]
    public void ChooseAction_ShooterCanApproachForAShotAcrossAnImpassableLowObstacle()
    {
        var actor = new BattleUnit("archer", BattleSide.Enemy, new(0, 0), 3, 10, 3,
            speed: 6, movement: 2, range: 3, isRanged: true);
        var target = new BattleUnit("player", BattleSide.Player, new(5, 0), 3, 10, 2,
            speed: 1, movement: 1, range: 1, isRanged: false);
        var map = new BattleMap(Enumerable.Range(0, 6).Select(q =>
            new BattleTile(new(q, 0), isPassable: q is not (3 or 4), blocksLineOfSight: false)));
        var battle = new Battle(map, [actor, target]);
        battle.StartRound();
        var ai = new BattleAi();
        var action = ai.ChooseAction(battle, actor.Id);
        Assert.Equal(new HexCoord(2, 0), action.Destination);
        Assert.True(battle.Move(actor.Id, action.Destination!.Value));
        Assert.Equal(BattleActionType.Attack, ai.ChooseAction(battle, actor.Id).Type);
        Assert.True(battle.Attack(actor.Id, target.Id));
    }

    [Fact]
    public void ChooseAction_FollowsDetourAcrossTurnsEvenWhenFirstStepDoesNotGetCloser()
    {
        var actor = new BattleUnit("ai", BattleSide.Enemy, new(0, 0), 3, 10, 3,
            speed: 6, movement: 1, range: 1, isRanged: false);
        var target = new BattleUnit("player", BattleSide.Player, new(3, 0), 3, 10, 2,
            speed: 1, movement: 1, range: 1, isRanged: false);
        HexCoord[] corridor = [new(0, 0), new(0, 1), new(1, 1), new(2, 1), new(3, 0)];
        var map = new BattleMap(corridor.Select(cell => new BattleTile(cell)));
        var battle = new Battle(map, [actor, target]);
        var ai = new BattleAi();
        battle.StartRound();

        foreach (var expected in corridor.Skip(1).Take(3))
        {
            var action = ai.ChooseAction(battle, actor.Id);
            Assert.Equal(BattleActionType.Move, action.Type);
            Assert.Equal(expected, action.Destination);
            Assert.True(battle.Move(actor.Id, action.Destination!.Value));
            battle.EndActivation();
            battle.EndActivation();
            battle.StartRound();
        }
        Assert.Equal(BattleActionType.Attack, ai.ChooseAction(battle, actor.Id).Type);
    }

    [Fact]
    public void ChooseAction_TakesRouteAwayFromTargetToEscapeADeadEnd()
    {
        var actor = new BattleUnit("ai", BattleSide.Enemy, new(0, 0), 3, 10, 3,
            speed: 6, movement: 1, range: 1, isRanged: false);
        var target = new BattleUnit("player", BattleSide.Player, new(2, 0), 3, 10, 2,
            speed: 1, movement: 1, range: 1, isRanged: false);
        HexCoord[] corridor = [new(0, 0), new(-1, 0), new(-2, 0), new(-2, 1),
            new(-2, 2), new(-1, 2), new(0, 2), new(1, 1), new(2, 0)];
        var battle = new Battle(new BattleMap(corridor.Select(cell => new BattleTile(cell))), [actor, target]);
        battle.StartRound();
        var action = new BattleAi().ChooseAction(battle, actor.Id);
        Assert.Equal(BattleActionType.Move, action.Type);
        Assert.Equal(new HexCoord(-1, 0), action.Destination);
        Assert.True(battle.Move(actor.Id, action.Destination!.Value));
    }

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

    [Fact]
    public void ChooseAction_WhenDirectRouteIsBlocked_MovesAlongShortestRouteToAttackPosition()
    {
        var ai = new BattleAi();
        var enemy = new BattleUnit("ai-melee", BattleSide.Enemy, new(0, 0), 3, 10, 3,
            speed: 6, movement: 1, range: 1, isRanged: false);
        var player = new BattleUnit("player", BattleSide.Player, new(2, 0), 3, 10, 2,
            speed: 1, movement: 1, range: 1, isRanged: false);
        var coordinates = new[] { new HexCoord(0, 0), new(0, 1), new(0, 2),
            new(1, 2), new(2, 1), new(2, 0) };
        var battle = new Battle(new BattleMap(coordinates.Select(c => new BattleTile(c))), new[] { enemy, player });
        battle.StartRound();

        var action = ai.ChooseAction(battle, "ai-melee");

        Assert.Equal(BattleActionType.Move, action.Type);
        Assert.Equal(new HexCoord(0, 1), action.Destination);
    }
}
