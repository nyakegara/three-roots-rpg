using Game.Core.Geometry;

namespace Game.Core.Combat;

/// <summary>Terrain on the tactical field; unit occupancy is tracked separately.</summary>
public sealed class BattleTile
{
    public HexCoord Coordinate { get; }
    public int Height { get; }
    public bool IsPassable { get; }

    public BattleTile(HexCoord coordinate, int height = 0, bool isPassable = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Coordinate = coordinate;
        Height = height;
        IsPassable = isPassable;
    }
}
