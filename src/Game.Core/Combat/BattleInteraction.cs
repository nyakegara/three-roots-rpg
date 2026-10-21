using Game.Core.Combat.AI;
using Game.Core.Combat.Pathfinding;
using Game.Core.Geometry;

namespace Game.Core.Combat;

public enum BattleCellAction { Move, Attack, RangedAttack, Heal, InRange, Blocked }

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

    /// <summary>Read-only action preview. Uses the same attack and path rules as actual commands.</summary>
    public BattleCellAction GetCellAction(HexCoord coordinate, bool healing = false)
    {
        if (!IsPlayerTurn || Battle.ActiveUnit is not { } active ||
            Battle.Map.GetTile(coordinate) is not { IsPassable: true } tile)
            return BattleCellAction.Blocked;
        var occupant = Battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Position == coordinate);
        if (healing)
            return active.Ability == UnitAbility.HealAlly && !Battle.HasAttacked(active.Id) &&
                occupant is { } ally && ally.Id != active.Id && ally.Side == active.Side &&
                ally.RemainingHitPoints < ally.MaximumHitPointsAtBattleStart
                ? BattleCellAction.Heal : BattleCellAction.Blocked;
        if (occupant is not null)
        {
            if (Battle.HasAttacked(active.Id) || !BattleRules.CanAttack(active, occupant, Battle.Map))
                return BattleCellAction.Blocked;
            return active.IsRanged || active.Ability == UnitAbility.RadiantModes && active.AttackMode == AttackMode.HolyFire
                ? BattleCellAction.RangedAttack : BattleCellAction.Attack;
        }
        if (!Battle.HasMoved(active.Id))
        {
            var occupied = Battle.Units.Where(unit => unit.IsAlive && unit.Id != active.Id)
                .Select(unit => unit.Position).ToHashSet();
            var path = _pathfinder.FindPath(Battle.Map, active.Position, coordinate, occupied);
            if (path.IsReachable && path.Cost <= active.Movement) return BattleCellAction.Move;
        }
        return !Battle.HasAttacked(active.Id) && BattleRules.CanAttackTile(active, tile, Battle.Map)
            ? BattleCellAction.InRange : BattleCellAction.Blocked;
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

    public bool TrySetAttackMode(AttackMode mode) => IsPlayerTurn &&
        Battle.TrySetAttackMode(Battle.ActiveUnit!.Id, mode);

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
            BattleActionType.SetAttackMode when action.Mode is { } mode => Battle.TrySetAttackMode(unit.Id, mode),
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
