using System.Numerics;
using Game.Core.World;

namespace Game.Core.Geometry;

/// <summary>Projects the axial travel graph into a broad, gently isometric adventure map.</summary>
public sealed class AdventureMapProjection
{
    private readonly WorldMap _map;
    private readonly float _tileWidth;
    private readonly float _tileHeight;
    private readonly float _elevationStep;
    private readonly float _minX;
    private readonly float _minY;

    public Vector2 WorldSize { get; }

    public AdventureMapProjection(WorldMap map, float tileWidth, float tileHeight, float elevationStep,
        float verticalPaddingTiles = 7)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(elevationStep);
        ArgumentOutOfRangeException.ThrowIfNegative(verticalPaddingTiles);
        if (map.Tiles.Count == 0) throw new ArgumentException("An adventure map needs at least one tile.", nameof(map));

        _map = map;
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;
        _elevationStep = elevationStep;
        var points = map.Tiles.Select(tile => ProjectRaw(tile.Coordinate, tile.Height)).ToArray();
        var horizontalPadding = tileWidth * 2;
        var verticalPadding = tileHeight * verticalPaddingTiles;
        _minX = points.Min(point => point.X) - horizontalPadding;
        _minY = points.Min(point => point.Y) - verticalPadding;
        WorldSize = new Vector2(points.Max(point => point.X) - points.Min(point => point.X) + horizontalPadding * 2,
            points.Max(point => point.Y) - points.Min(point => point.Y) + verticalPadding * 2);
    }

    public Vector2 Project(HexCoord coordinate, int height = 0)
    {
        var position = ProjectRaw(coordinate, height) - new Vector2(_minX, _minY);
        return _map.Id == "flora-01" ? FloraIslandArtAlignment.Warp(position, WorldSize) : position;
    }

    public bool TryPick(Vector2 worldPosition, out HexCoord coordinate)
    {
        var candidate = _map.Tiles
            .Select(tile => (Tile: tile, Center: Project(tile.Coordinate, tile.Height)))
            .Where(item => MathF.Abs(worldPosition.X - item.Center.X) <= _tileWidth / 2 &&
                           MathF.Abs(worldPosition.Y - item.Center.Y) <= _tileHeight / 2)
            .OrderBy(item => Vector2.DistanceSquared(worldPosition, item.Center))
            .FirstOrDefault();
        if (candidate.Tile is null)
        {
            coordinate = default;
            return false;
        }

        coordinate = candidate.Tile.Coordinate;
        return true;
    }

    private Vector2 ProjectRaw(HexCoord coordinate, int height) =>
        new((coordinate.Q - coordinate.R) * _tileWidth / 2,
            (coordinate.Q + coordinate.R) * _tileHeight / 2 - height * _elevationStep);
}
