using Game.Core.Geometry;

namespace Game.Core.Combat;

public enum AttackFailureReason { None, UnavailableUnit, FriendlyTarget, ImpassableTarget, NotAdjacent, OutOfRange, BlockedLineOfSight }

public static class BattleRules
{
    public const int HolyFireRange = 6;
    private const int RangedUnitEyeHeight = 1;

    /// <summary>Whether a tile lies in this unit's current attack area, before checking its occupant.</summary>
    public static bool CanAttackTile(BattleUnit attacker, BattleTile target, BattleMap map)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(map);
        return GetTileAttackFailureReason(attacker, target, map) == AttackFailureReason.None;
    }

    public static bool IsWithinAttackRange(BattleUnit attacker, BattleTile target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        if (!attacker.IsAlive) return false;
        var distance = attacker.Position.DistanceTo(target.Coordinate);
        if (distance == 0) return false;
        if (attacker.Ability == UnitAbility.SacrificePulse) return distance <= 4;
        var ranged = attacker.IsRanged || attacker.Ability == UnitAbility.RadiantModes &&
            attacker.AttackMode == AttackMode.HolyFire;
        return ranged ? distance <= GetEffectiveRange(attacker, target) : distance == 1;
    }

    public static bool IsRangedShotBlocked(BattleUnit attacker, BattleTile target, BattleMap map)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(map);
        return target.IsPassable && IsWithinAttackRange(attacker, target) &&
               !CanAttackTile(attacker, target, map);
    }

    public static AttackFailureReason GetAttackFailureReason(BattleUnit attacker, BattleUnit target, BattleMap map)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(map);
        if (!attacker.IsAlive) return AttackFailureReason.UnavailableUnit;
        if (!target.IsAlive) return AttackFailureReason.UnavailableUnit;
        if (attacker.Side == target.Side) return AttackFailureReason.FriendlyTarget;
        var targetTile = map.GetTile(target.Position);
        if (map.GetTile(attacker.Position)?.IsPassable != true || targetTile?.IsPassable != true)
            return AttackFailureReason.ImpassableTarget;
        return GetTileAttackFailureReason(attacker, targetTile, map);
    }

    private static AttackFailureReason GetTileAttackFailureReason(BattleUnit attacker, BattleTile target, BattleMap map)
    {
        if (!attacker.IsAlive) return AttackFailureReason.UnavailableUnit;
        if (!target.IsPassable || map.GetTile(attacker.Position)?.IsPassable != true)
            return AttackFailureReason.ImpassableTarget;
        var distance = attacker.Position.DistanceTo(target.Coordinate);
        if (distance == 0) return AttackFailureReason.UnavailableUnit;
        if (attacker.Ability == UnitAbility.SacrificePulse)
            return distance <= 4 ? AttackFailureReason.None : AttackFailureReason.OutOfRange;
        var ranged = attacker.IsRanged || attacker.Ability == UnitAbility.RadiantModes &&
            attacker.AttackMode == AttackMode.HolyFire;
        if (!ranged && distance != 1) return AttackFailureReason.NotAdjacent;
        if (ranged && distance > GetEffectiveRange(attacker, target)) return AttackFailureReason.OutOfRange;
        if (ranged && !HasLineOfSight(attacker.Position, attacker.Height, target.Coordinate, target.Height, map))
            return AttackFailureReason.BlockedLineOfSight;
        return AttackFailureReason.None;
    }

    public static int GetEffectiveRange(BattleUnit attacker, BattleTile target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        var baseRange = attacker.Ability == UnitAbility.RadiantModes && attacker.AttackMode == AttackMode.HolyFire
            ? HolyFireRange : attacker.Ability == UnitAbility.SacrificePulse ? 4 : attacker.Range;
        if (attacker.Ability == UnitAbility.SacrificePulse) return baseRange;
        return baseRange + (attacker.Height > target.Height && (attacker.IsRanged ||
            attacker.Ability == UnitAbility.RadiantModes && attacker.AttackMode == AttackMode.HolyFire) ? 2 : 0);
    }

    public static long GetBaseAttackDamage(BattleUnit attacker) =>
        attacker.Ability == UnitAbility.RadiantModes && attacker.AttackMode == AttackMode.HolyFire
            ? (long)attacker.Count * 8 : attacker.TotalDamage;

    /// <summary>Damage before applying target health, shared by the command and its preview.</summary>
    public static long GetAttackDamage(BattleUnit attacker, BattleUnit target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        var rangedTarget = target.IsRanged || target.Ability == UnitAbility.RadiantModes &&
            target.AttackMode == AttackMode.HolyFire;
        return attacker.Ability == UnitAbility.Hunter && rangedTarget
            ? checked(GetBaseAttackDamage(attacker) * 3 / 2) : GetBaseAttackDamage(attacker);
    }

    public static bool CanAttack(BattleUnit attacker, BattleUnit target, BattleMap map)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(map);
        return GetAttackFailureReason(attacker, target, map) == AttackFailureReason.None;
    }

    public static bool HasLineOfSight(HexCoord start, int startHeight, HexCoord end, int endHeight, BattleMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var distance = start.DistanceTo(end);
        if (distance <= 1)
        {
            return true;
        }

        for (var step = 1; step < distance; step++)
        {
            var tile = map.GetTile(Interpolate(start, end, (double)step / distance));
            // Trace from a unit's body height above terrain, not from the ground.
            // Without this offset, a same-level plateau between a high shooter and
            // a lower target incorrectly blocks every descending shot.
            if (tile is null || tile.BlocksLineOfSight ||
                tile.Height > startHeight + RangedUnitEyeHeight +
                ((double)endHeight - startHeight) * step / distance)
            {
                return false;
            }
        }

        return true;
    }

    private static HexCoord Interpolate(HexCoord start, HexCoord end, double amount)
    {
        var x = start.Q + ((long)end.Q - start.Q) * amount;
        var z = start.R + ((long)end.R - start.R) * amount;
        var y = -x - z;
        var roundedX = Math.Round(x);
        var roundedY = Math.Round(y);
        var roundedZ = Math.Round(z);
        var differenceX = Math.Abs(roundedX - x);
        var differenceY = Math.Abs(roundedY - y);
        var differenceZ = Math.Abs(roundedZ - z);
        if (differenceX > differenceY && differenceX > differenceZ)
        {
            roundedX = -roundedY - roundedZ;
        }
        else if (differenceY > differenceZ)
        {
            roundedY = -roundedX - roundedZ;
        }
        else
        {
            roundedZ = -roundedX - roundedY;
        }

        return new HexCoord(checked((int)roundedX), checked((int)roundedZ));
    }
}
