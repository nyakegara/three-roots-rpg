using Game.Core.Combat;
using Game.Core.Combat.Generation;
using Game.Core.Geometry;
using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Shared field layers and projection for preview and playable combat.</summary>
internal sealed class BattlefieldRenderer
{
    private readonly PixelCanvas _canvas;
    private readonly AssetCatalog _assets;
    private readonly BattleMap _map;
    private readonly Dictionary<HexCoord, BattlefieldPropKind> _propsByCoordinate;
    private static readonly int[] SharedEdgeByNeighbor = [1, 0, 5, 4, 3, 2];

    public HexLayout Layout { get; } = new();
    public IReadOnlyList<BattleTile> DrawOrder { get; }

    public BattlefieldRenderer(PixelCanvas canvas, AssetCatalog assets, BattleMap map,
        IReadOnlyList<BattlefieldProp> props)
    {
        _canvas = canvas;
        _assets = assets;
        _map = map;
        _propsByCoordinate = props.ToDictionary(prop => prop.Coordinate, prop => prop.Kind);
        DrawOrder = map.Tiles.OrderBy(tile => tile.Coordinate.R)
            .ThenBy(tile => tile.Coordinate.Q).ToArray();
    }

    public HexCoord? HitTest(Point point) => Layout.HitTest(point, DrawOrder);

    public void DrawTerrain(bool showGrid, float nightBlend)
    {
        BattlefieldBackdrop.Draw(_canvas, _assets, nightBlend);

        if (showGrid)
        {
            using (_canvas.PushTint(BattleLighting.AmbientTint(nightBlend)))
                DrawElevationSurface();
        }

        // Height walls remain visible even when the tactical grid is hidden.
        DrawElevationEdges();

        if (showGrid)
            foreach (var tile in DrawOrder)
                _canvas.Outline(Layout.Corners(tile, 1), Translucent(188, 193, 133, 85), 1);

    }

    public void DrawProps(float nightBlend)
    {
        // Terrain props receive the full ambient night tint.
        using (_canvas.PushTint(BattleLighting.ForegroundTint(nightBlend)))
        foreach (var tile in DrawOrder)
        {
            var center = Layout.Center(tile);
            var x = (int)center.X;
            var y = (int)center.Y;
            if (_propsByCoordinate.TryGetValue(tile.Coordinate, out var kind))
                BattlefieldPropArt.Draw(_canvas, kind, x, y);
            else if (!tile.IsPassable)
                DrawRock(x, y);
        }

    }

    private void DrawElevationSurface()
    {
        foreach (var tile in DrawOrder)
        {
            var tint = tile.Height switch
            {
                0 => Translucent(62, 91, 54, 34),
                1 => Translucent(176, 143, 68, 78),
                _ => Translucent(239, 184, 75, 110)
            };
            _canvas.Polygon(Layout.Corners(tile, 4), tint);
        }
    }

    private void DrawElevationEdges()
    {
        foreach (var tile in DrawOrder)
        {
            var corners = Layout.Corners(tile);
            var neighbors = tile.Coordinate.GetNeighbors();
            for (var direction = 0; direction < neighbors.Count; direction++)
            {
                var neighbor = _map.GetTile(neighbors[direction]);
                if (neighbor is null || tile.Height <= neighbor.Height)
                    continue;

                var edge = SharedEdgeByNeighbor[direction];
                var first = corners[edge];
                var second = corners[(edge + 1) % corners.Length];
                var difference = tile.Height - neighbor.Height;
                var midpoint = (first + second) / 2;
                var inward = Vector2.Normalize(Layout.Center(tile) - midpoint);

                // A dark earthen face with a thin grass rim marks only actual level changes.
                _canvas.Line(first, second, Translucent(83, 56, 36, 238), 3 + difference);
                _canvas.Line(first + inward * 2, second + inward * 2,
                    Translucent(163, 171, 94, 215), 1);
            }
        }
    }

    private void DrawRock(int x, int y)
    {
        if (!_assets.TryDraw(_canvas, AssetCatalog.Rock, new Rectangle(x - 28, y - 28, 56, 48)))
            FantasyArt.DrawRock(_canvas, x, y);
    }

    private static Color Translucent(int red, int green, int blue, int alpha) =>
        new Color(red, green, blue) * (alpha / 255f);
}
