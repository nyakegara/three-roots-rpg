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
    private readonly bool _mountain;
    private readonly Dictionary<HexCoord, BattlefieldPropKind> _propsByCoordinate;
    private static readonly int[] SharedEdgeByNeighbor = [1, 0, 5, 4, 3, 2];

    public HexLayout Layout { get; }
    public IReadOnlyList<BattleTile> DrawOrder { get; }

    public BattlefieldRenderer(PixelCanvas canvas, AssetCatalog assets, BattleMap map,
        IReadOnlyList<BattlefieldProp> props, bool mountain = false)
    {
        _canvas = canvas;
        _assets = assets;
        _map = map;
        _mountain = mountain;
        Layout = new HexLayout { ElevationStep = mountain ? 14 : 2 };
        _propsByCoordinate = props.ToDictionary(prop => prop.Coordinate, prop => prop.Kind);
        DrawOrder = map.Tiles.OrderBy(tile => tile.Coordinate.R)
            .ThenBy(tile => tile.Coordinate.Q).ToArray();
    }

    public HexCoord? HitTest(Point point) => Layout.HitTest(point, DrawOrder);

    public void DrawTerrain(bool showGrid, float nightBlend)
    {
        BattlefieldBackdrop.Draw(_canvas, _assets, nightBlend);

        if (showGrid || _mountain)
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
            if (_mountain)
                tint = tile.Height switch
                {
                    2 => new Color(153, 149, 123) * 0.92f,
                    1 => new Color(139, 125, 94) * 0.88f,
                    _ => new Color(118, 111, 87) * 0.7f
                };
            _canvas.Polygon(Layout.Corners(tile, _mountain ? 0 : 4), tint);
            if (_mountain)
            {
                var center = Layout.Center(tile);
                for (var fleck = 0; fleck < 16; fleck++)
                {
                    var seed = Math.Abs(tile.Coordinate.Q * 157 + tile.Coordinate.R * 83 + fleck * 47);
                    var x = (int)center.X + seed % 57 - 28;
                    var y = (int)center.Y + (seed / 7) % 31 - 15;
                    _canvas.Line(new Vector2(x, y), new Vector2(x + 2 + seed % 3, y),
                        fleck % 2 == 0 ? new Color(179, 171, 139) * 0.3f : new Color(64, 65, 52) * 0.2f, 1);
                }
            }
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

                if (_mountain)
                {
                    var drop = new Vector2(0, difference * Layout.ElevationStep);
                    _canvas.Polygon([first, second, second + drop, first + drop], new Color(79, 76, 63));
                    for (var seam = 1; seam <= difference * 3; seam++)
                    {
                        var offset = new Vector2(0, seam * 4);
                        _canvas.Line(first + offset, second + offset, new Color(107, 101, 79), 1);
                    }
                }
                // A dark earthen face with a thin grass rim marks only actual level changes.
                _canvas.Line(first, second, (_mountain ? new Color(72, 72, 64) : Translucent(83, 56, 36, 238)), _mountain ? 2 : 3 + difference);
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
