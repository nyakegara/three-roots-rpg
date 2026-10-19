using Game.Core.Combat.AI;
using Game.Core.Combat.Pathfinding;
using Game.Core.Geometry;

namespace Game.Core.Combat;

/// <summary>Two-click movement and one-step opponent turns, independent of graphics and input devices.</summary>
public sealed class BattleInteraction
{
    private readonly AStarPathfinder _pathfinder = new();
    private readonly BattleAi _opponent = new();

    public Battle Battle { get; }
    public HexCoord? SelectedDestination { get; private set; }
    public PathResult SelectedPath { get; private set; } = new(false, [], 0);
    public bool IsPlayerTurn => !Battle.IsFinished && Battle.ActiveUnit is { IsAlive: true, Side: BattleSide.Player };

    public BattleInteraction(Battle battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        Battle = battle;
        BeginNextRoundIfNeeded();
    }

    public bool SelectDestination(HexCoord destination)
    {
        if (!IsPlayerTurn || Battle.ActiveUnit is not { } unit)
            return false;

        if (SelectedDestination == destination && Battle.Move(unit.Id, destination))
        {
            ClearSelection();
            return true;
        }

        SelectedDestination = destination;
        var occupied = Battle.Units.Where(other => other.IsAlive && other.Id != unit.Id)
            .Select(other => other.Position).ToHashSet();
        SelectedPath = _pathfinder.FindPath(Battle.Map, unit.Position, destination, occupied);
        return false;
    }

    public bool TryAttack(string targetId)
    {
        if (!IsPlayerTurn || !Battle.Attack(Battle.ActiveUnit!.Id, targetId))
            return false;
        ClearSelection();
        return true;
    }

    public bool TryHeal(string targetId)
    {
        if (!IsPlayerTurn || !Battle.TryHeal(Battle.ActiveUnit!.Id, targetId))
            return false;
        ClearSelection();
        return true;
    }

    public bool EndPlayerTurn()
    {
        if (!IsPlayerTurn)
            return false;
        EndActivation();
        return true;
    }

    public BattleAction? StepOpponent()
    {
        if (Battle.IsFinished || Battle.ActiveUnit is not { Side: BattleSide.Enemy } unit)
            return null;

        var action = _opponent.ChooseAction(Battle, unit.Id);
        var applied = action.Type switch
        {
            BattleActionType.Move when action.Destination is { } destination => Battle.Move(unit.Id, destination),
            BattleActionType.Attack when action.TargetId is { } target => Battle.Attack(unit.Id, target),
            _ => false
        };
        if (!applied)
        {
            EndActivation();
            return BattleAction.End();
        }
        return action;
    }

    private void EndActivation()
    {
        ClearSelection();
        Battle.EndActivation();
        BeginNextRoundIfNeeded();
    }

    private void BeginNextRoundIfNeeded()
    {
        if (!Battle.IsFinished && Battle.ActiveUnit is null)
            Battle.StartRound();
    }

    private void ClearSelection()
    {
        SelectedDestination = null;
        SelectedPath = new PathResult(false, [], 0);
    }
}
