using Game.Core.Geometry;

namespace Game.Core.Combat;

/// <summary>Immutable tactical terrain, indexed by axial coordinates.</summary>
public sealed class BattleMap
{
    private readonly Dictionary<HexCoord, BattleTile> _tiles = new();

    public IReadOnlyCollection<BattleTile> Tiles => _tiles.Values;

    public BattleMap(IEnumerable<BattleTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        foreach (var tile in tiles)
        {
            if (tile is null)
            {
                throw new ArgumentException("A battle map cannot contain null tiles.", nameof(tiles));
            }

            if (!_tiles.TryAdd(tile.Coordinate, tile))
            {
                throw new ArgumentException(
                    $"Duplicate battle cell ({tile.Coordinate.Q}, {tile.Coordinate.R}).", nameof(tiles));
            }
        }
    }

    public bool Contains(HexCoord coordinate) => _tiles.ContainsKey(coordinate);

    public BattleTile? GetTile(HexCoord coordinate) => _tiles.GetValueOrDefault(coordinate);
}
