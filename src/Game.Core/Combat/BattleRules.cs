using Game.Core.Geometry;

namespace Game.Core.Combat;

public static class BattleRules
{
    public const int HolyFireRange = 6;

    /// <summary>Whether a tile lies in this unit's current attack area, before checking its occupant.</summary>
    public static bool CanAttackTile(BattleUnit attacker, BattleTile target, BattleMap map)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(map);
        if (!attacker.IsAlive || !target.IsPassable || map.GetTile(attacker.Position)?.IsPassable != true)
            return false;

        var distance = attacker.Position.DistanceTo(target.Coordinate);
        if (distance == 0) return false;
        var ranged = attacker.IsRanged || attacker.Ability == UnitAbility.RadiantModes &&
            attacker.AttackMode == AttackMode.HolyFire;
        return ranged
            ? distance <= GetEffectiveRange(attacker, target) &&
              HasLineOfSight(attacker.Position, attacker.Height, target.Coordinate, target.Height, map)
            : distance == 1;
    }

    public static int GetEffectiveRange(BattleUnit attacker, BattleTile target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        var baseRange = attacker.Ability == UnitAbility.RadiantModes && attacker.AttackMode == AttackMode.HolyFire
            ? HolyFireRange : attacker.Range;
        return baseRange + (attacker.Height > target.Height && (attacker.IsRanged ||
            attacker.Ability == UnitAbility.RadiantModes && attacker.AttackMode == AttackMode.HolyFire) ? 2 : 0);
    }

    public static bool CanAttack(BattleUnit attacker, BattleUnit target, BattleMap map)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(map);
        var attackerTile = map.GetTile(attacker.Position);
        var targetTile = map.GetTile(target.Position);
        if (!attacker.IsAlive || !target.IsAlive || attacker.Side == target.Side ||
            attackerTile?.IsPassable != true || targetTile?.IsPassable != true)
        {
            return false;
        }

        return CanAttackTile(attacker, targetTile, map);
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
            if (tile is null || tile.BlocksLineOfSight ||
                tile.Height > startHeight + ((double)endHeight - startHeight) * step / distance)
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
