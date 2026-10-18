using Game.Core.Geometry;

namespace Game.Core.World;

/// <summary>
/// Stores existing map cells, including impassable terrain.
/// A coordinate without a cell is outside the map.
/// </summary>
public sealed class WorldMap
{
    private readonly Dictionary<HexCoord, WorldTile> _tiles = new();

    public IReadOnlyCollection<WorldTile> Tiles => _tiles.Values;

    public WorldMap(IEnumerable<WorldTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        foreach (var tile in tiles)
        {
            if (tile is null)
            {
                throw new ArgumentException("A map cannot contain null tiles.", nameof(tiles));
            }

            if (!_tiles.TryAdd(tile.Coordinate, tile))
            {
                throw new ArgumentException(
                    $"Map contains duplicate coordinates ({tile.Coordinate.Q}, {tile.Coordinate.R}).",
                    nameof(tiles));
            }
        }
    }

    public bool Contains(HexCoord coordinate)
    {
        return _tiles.ContainsKey(coordinate);
    }

    public WorldTile? GetTile(HexCoord coordinate)
    {
        return _tiles.GetValueOrDefault(coordinate);
    }
}
