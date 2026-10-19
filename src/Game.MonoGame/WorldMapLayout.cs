using System.Numerics;
using Game.Core.Geometry;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Vector2 = System.Numerics.Vector2;

namespace Game.MonoGame;

/// <summary>Fits a JSON world map into the field above the lower wooden HUD.</summary>
internal sealed class WorldMapLayout
{
    private const float VerticalCompression = 0.76f;
    private const float ElevationStep = 15;
    private readonly HexProjection _projection;

    private WorldMapLayout(HexProjection projection, float radius)
    {
        _projection = projection;
        Radius = radius;
    }

    public float Radius { get; }

    public static WorldMapLayout Fit(WorldMap map, Rectangle area)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Tiles.Count == 0)
            throw new ArgumentException("A world map must contain at least one tile.", nameof(map));

        var margin = 20f;
        var axialWidth = map.Width - 1 + (map.Height - 1) / 2f;
        var maxHeight = map.Tiles.Max(tile => tile.Height);
        var radiusByWidth = (area.Width - margin * 2) / (MathF.Sqrt(3) * axialWidth + 2);
        var radiusByHeight = (area.Height - margin * 2 - maxHeight * ElevationStep) /
                             (VerticalCompression * (1.5f * (map.Height - 1) + 2));
        var radius = MathF.Min(radiusByWidth, radiusByHeight);
        if (radius <= 0)
            throw new ArgumentException("Map does not fit in the available screen area.", nameof(area));

        var projectedWidth = MathF.Sqrt(3) * radius * axialWidth;
        var originX = area.Center.X - projectedWidth / 2;
        var lowestCenterY = map.Tiles.Min(tile =>
            1.5f * radius * VerticalCompression * tile.Coordinate.R - tile.Height * ElevationStep);
        var originY = area.Top + radius * VerticalCompression - lowestCenterY;
        return new WorldMapLayout(
            new HexProjection(radius, VerticalCompression, ElevationStep, new Vector2(originX, originY)),
            radius);
    }

    public Vector2 Center(WorldTile tile) => _projection.Center(tile.Coordinate, tile.Height);

    public IReadOnlyList<Vector2> Corners(WorldTile tile, float inset = 0) =>
        _projection.Corners(tile.Coordinate, tile.Height, inset);

    public HexCoord? HitTest(Point point, IReadOnlyList<WorldTile> drawOrder)
    {
        var location = new Vector2(point.X, point.Y);
        for (var index = drawOrder.Count - 1; index >= 0; index--)
        {
            var tile = drawOrder[index];
            if (_projection.Contains(tile.Coordinate, location, tile.Height))
                return tile.Coordinate;
        }

        return null;
    }
}
