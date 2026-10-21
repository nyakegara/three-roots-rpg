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
    public string Id { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public HexCoord? StartPosition { get; }
    public IReadOnlyList<WorldPointOfInterest> PointsOfInterest { get; }
    public Game.Core.Navigation.AdventureNavigation? Navigation { get; }

    public WorldMap(IEnumerable<WorldTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        Id = string.Empty;
        Name = string.Empty;
        Width = 0;
        Height = 0;
        StartPosition = null;
        PointsOfInterest = Array.Empty<WorldPointOfInterest>();
        AddTiles(tiles);
    }

    public WorldMap(string id, string name, int width, int height, HexCoord? startPosition,
        IEnumerable<WorldTile> tiles, IEnumerable<WorldPointOfInterest>? pointsOfInterest = null,
        Game.Core.Navigation.AdventureNavigation? navigation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(tiles);

        Id = id;
        Name = name;
        Width = width;
        Height = height;
        StartPosition = startPosition;
        Navigation = navigation;
        PointsOfInterest = Array.AsReadOnly((pointsOfInterest ?? Array.Empty<WorldPointOfInterest>()).ToArray());

        AddTiles(tiles);
    }

    private void AddTiles(IEnumerable<WorldTile> tiles)
    {
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
