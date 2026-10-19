using Game.Core.Geometry;

namespace Game.Core.World;

/// <summary>
/// Stores the terrain properties of one hexagon on the exploration map.
/// </summary>
public sealed class WorldTile
{
    public HexCoord Coordinate { get; }
    public int Height { get; }
    public bool IsPassable { get; }
    public string TerrainId { get; }

    public WorldTile(HexCoord coordinate, int height = 0, bool isPassable = true, string terrainId = "grass")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentException.ThrowIfNullOrWhiteSpace(terrainId);

        Coordinate = coordinate;
        Height = height;
        IsPassable = isPassable;
        TerrainId = terrainId;
    }
}
