using Game.Core.Geometry;

namespace Game.Core.Combat;

/// <summary>A living troop stack with shared damage and a creature count.</summary>
public sealed class BattleUnit
{
    private int _remainingHitPoints;

    public string Id { get; }
    public BattleSide Side { get; }
    public HexCoord Position { get; private set; }
    public int Height { get; private set; }
    public int Count => _remainingHitPoints == 0 ? 0 : (int)(((long)_remainingHitPoints + HitPointsPerCreature - 1) / HitPointsPerCreature);
    public int HitPointsPerCreature { get; }
    public int RemainingHitPoints => _remainingHitPoints;
    public int CurrentCreatureHitPoints => _remainingHitPoints == 0 ? 0 : (_remainingHitPoints - 1) % HitPointsPerCreature + 1;
    public int DamagePerCreature { get; }
    public int Speed { get; }
    public int Movement { get; }
    public int Range { get; }
    public bool IsRanged { get; }
    public bool IsHero { get; }
    public bool IsAlive => _remainingHitPoints > 0;

    public BattleUnit(string id, BattleSide side, HexCoord position, int count,
        int hitPointsPerCreature, int damagePerCreature, int speed, int movement, int range,
        bool isRanged, bool isHero = false, int height = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitPointsPerCreature);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(damagePerCreature);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(speed);
        ArgumentOutOfRangeException.ThrowIfNegative(movement);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Id = id;
        Side = side;
        Position = position;
        Height = height;
        HitPointsPerCreature = hitPointsPerCreature;
        _remainingHitPoints = checked(count * hitPointsPerCreature);
        DamagePerCreature = damagePerCreature;
        Speed = speed;
        Movement = movement;
        Range = range;
        IsRanged = isRanged;
        IsHero = isHero;
    }

    internal long TotalDamage => (long)Count * DamagePerCreature;

    internal void MoveTo(BattleTile tile)
    {
        Position = tile.Coordinate;
        Height = tile.Height;
    }

    internal void ApplyDamage(long damage)
    {
        if (damage <= 0 || !IsAlive)
        {
            return;
        }

        _remainingHitPoints = (int)Math.Max(0L, _remainingHitPoints - damage);
    }
}
