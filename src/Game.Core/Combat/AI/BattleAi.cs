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
            if (unit.Ability == UnitAbility.HealAlly)
            {
                // Spending an attack to restore a scratch is worse than attacking. Reserve
                // healing for a substantial wound, including a lost 8-HP Utburd.
                var heal = BattleRules.GetHealingAmount(unit);
                var wounded = battle.Units.Where(ally => ally.IsAlive && ally.Side == unit.Side &&
                                  ally.Id != unit.Id && ally.MaximumHitPointsAtBattleStart - ally.RemainingHitPoints >=
                                  Math.Max(3, heal - 3))
                    .OrderByDescending(ally => Math.Min(heal, ally.MaximumHitPointsAtBattleStart - ally.RemainingHitPoints))
                    .ThenBy(ally => ally.Id, StringComparer.Ordinal).FirstOrDefault();
                if (wounded is not null) return BattleAction.Heal(wounded.Id);
            }
            if (unit.Ability == UnitAbility.RadiantModes)
            {
                // Keep the hammer against adjacent enemies; use fire when a clear shot is available.
                var desired = battle.GetEnemies(unit.Side).Any(enemy =>
                    unit.Position.DistanceTo(enemy.Position) == 1) ? AttackMode.Hammer : AttackMode.HolyFire;
                if (desired != unit.AttackMode)
                    return BattleAction.SetMode(desired);
            }
            var target = battle.GetEnemies(unit.Side)
                .Where(enemy => BattleRules.CanAttack(unit, enemy, battle.Map))
                .OrderBy(enemy => unit.Ability == UnitAbility.Hunter && IsRangedThreat(enemy) ? 0 : 1)
                .ThenBy(enemy => enemy.Count)
                .ThenBy(enemy => enemy.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target is not null)
            {
                return BattleAction.Attack(target.Id);
            }
        }

        if (battle.HasMoved(unitId) || unit.Movement == 0)
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
        // Follow a real route to an enemy, even if its first step stays equally far
        // away or goes backwards around a wall. Straight distance alone can stall.
        var approach = battle.Map.Tiles.Where(tile => tile.IsPassable && !occupied.Contains(tile.Coordinate) &&
                enemies.Any(enemy => tile.Coordinate.DistanceTo(enemy.Position) == 1))
            .Select(tile => (Path: _pathfinder.FindPath(battle.Map, unit.Position, tile.Coordinate, occupied),
                RangedTarget: enemies.Any(enemy => IsRangedThreat(enemy) &&
                    tile.Coordinate.DistanceTo(enemy.Position) == 1)))
            .Where(candidate => candidate.Path.IsReachable)
            .OrderBy(candidate => unit.Ability == UnitAbility.Hunter && candidate.RangedTarget ? 0 : 1)
            .ThenBy(candidate => candidate.Path.Cost)
            .ThenBy(candidate => candidate.Path.Coordinates[^1].Q)
            .ThenBy(candidate => candidate.Path.Coordinates[^1].R)
            .FirstOrDefault();
        if (approach.Path is { Cost: > 0 })
            return BattleAction.MoveTo(approach.Path.Coordinates[Math.Min(unit.Movement, approach.Path.Cost)]);
        if (approach.Path is not null || !(unit.IsRanged || unit.Ability == UnitAbility.RadiantModes &&
                                     unit.AttackMode == AttackMode.HolyFire))
            return BattleAction.End();

        // A shooter may still approach an isolated enemy without a walking route
        // to its neighbors. Keep the existing distance-based fallback in that case.
        var candidates = new List<(HexCoord Coordinate, int PathCost, int Distance)>
        {
            (unit.Position, 0, enemies.Min(enemy => unit.Position.DistanceTo(enemy.Position)))
        };

        foreach (var tile in battle.Map.Tiles.OrderBy(tile => tile.Coordinate.Q).ThenBy(tile => tile.Coordinate.R))
        {
            if (tile.Coordinate == unit.Position || occupied.Contains(tile.Coordinate))
            {
                continue;
            }

            var path = _pathfinder.FindPath(battle.Map, unit.Position, tile.Coordinate, occupied);
            if (path.IsReachable && path.Cost <= unit.Movement)
            {
                candidates.Add((tile.Coordinate, path.Cost,
                    enemies.Min(enemy => tile.Coordinate.DistanceTo(enemy.Position))));
            }
        }

        var best = candidates.OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.PathCost)
            .ThenBy(candidate => candidate.Coordinate.Q)
            .ThenBy(candidate => candidate.Coordinate.R)
            .First();
        return best.Coordinate == unit.Position ? BattleAction.End() : BattleAction.MoveTo(best.Coordinate);
    }

    private static bool IsRangedThreat(BattleUnit unit) => unit.IsRanged ||
        unit.Ability == UnitAbility.RadiantModes && unit.AttackMode == AttackMode.HolyFire;
}
