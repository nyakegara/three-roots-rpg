using Game.Core.Combat.Pathfinding;
using Game.Core.Geometry;

namespace Game.Core.Combat.AI;

/// <summary>Deterministic tactical opponent: attack when possible, otherwise approach the nearest enemy.</summary>
public sealed class BattleAi
{
    private readonly AStarPathfinder _pathfinder = new();

    public BattleAction ChooseAction(Battle battle, string unitId)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentException.ThrowIfNullOrWhiteSpace(unitId);
        var unit = battle.Units.FirstOrDefault(candidate => candidate.Id == unitId && candidate.IsAlive);
        if (unit is null || battle.ActiveUnit?.Id != unitId)
        {
            return BattleAction.End();
        }

        if (!battle.HasAttacked(unitId))
        {
            var target = battle.GetEnemies(unit.Side)
                .Where(enemy => BattleRules.CanAttack(unit, enemy, battle.Map))
                .OrderBy(enemy => enemy.Count)
                .ThenBy(enemy => enemy.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target is not null)
            {
                return BattleAction.Attack(target.Id);
            }
        }

        if (battle.HasMoved(unitId))
        {
            return BattleAction.End();
        }

        var enemies = battle.GetEnemies(unit.Side);
        if (enemies.Count == 0)
        {
            return BattleAction.End();
        }

        var occupied = battle.Units.Where(other => other.IsAlive && other.Id != unitId)
            .Select(other => other.Position).ToHashSet();
        PathResult? bestPath = null;
        foreach (var tile in battle.Map.Tiles.OrderBy(tile => tile.Coordinate.Q).ThenBy(tile => tile.Coordinate.R))
        {
            if (!tile.IsPassable || occupied.Contains(tile.Coordinate) ||
                !enemies.Any(enemy => CanAttackFrom(unit, tile, enemy, battle.Map)))
            {
                continue;
            }

            var path = _pathfinder.FindPath(battle.Map, unit.Position, tile.Coordinate, occupied);
            if (path.IsReachable && path.Cost > 0 &&
                (bestPath is null || path.Cost < bestPath.Cost ||
                 (path.Cost == bestPath.Cost && IsEarlier(path.Coordinates[^1], bestPath.Coordinates[^1]))))
            {
                bestPath = path;
            }
        }

        if (bestPath is null || unit.Movement == 0)
        {
            return BattleAction.End();
        }

        var steps = Math.Min(unit.Movement, bestPath.Cost);
        return BattleAction.MoveTo(bestPath.Coordinates[steps]);
    }

    private static bool CanAttackFrom(BattleUnit attacker, BattleTile from, BattleUnit target, BattleMap map)
    {
        var distance = from.Coordinate.DistanceTo(target.Position);
        if (!attacker.IsRanged)
        {
            return distance == 1;
        }

        var targetTile = map.GetTile(target.Position);
        return targetTile is not null && distance <= attacker.Range + (from.Height > targetTile.Height ? 2 : 0) &&
               BattleRules.HasLineOfSight(from.Coordinate, from.Height, target.Position, targetTile.Height, map);
    }

    private static bool IsEarlier(HexCoord left, HexCoord right) =>
        left.Q < right.Q || (left.Q == right.Q && left.R < right.R);
}
