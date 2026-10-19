using Game.Core.Combat;
using Game.Core.Characters;
using Game.Core.Geometry;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame.Screens;

/// <summary>Campaign map view with a two-click route preview and confirm interaction.</summary>
internal sealed class ExplorationScreen : IGameScreen
{
    public static readonly Color Background = new(67, 89, 42);
    private const int HudTop = 600;
    private static readonly Color Shadow = new(27, 24, 16);
    private static readonly Color Bronze = new(133, 107, 63);
    private static readonly Color Highlight = new(201, 172, 102);
    private static readonly Color Text = new(221, 211, 174);
    private static readonly Color Muted = new(164, 158, 129);
    private static readonly int[] SharedEdgeByNeighbor = [1, 0, 5, 4, 3, 2];
    private static readonly Rectangle GridButton = new(1110, 18, 150, 42);

    private readonly PixelCanvas _canvas;
    private readonly PixelFont _font;
    private readonly AssetCatalog _assets;
    private readonly WorldMap _map;
    private readonly IReadOnlyList<WorldTile> _drawOrder;
    private readonly IReadOnlyDictionary<HexCoord, WorldPointOfInterest> _pointsByCoordinate;
    private readonly WorldMapLayout _layout;
    private readonly MovementSelection _movement;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private HexCoord? _hover;
    private string? _interactionMessage;
    private bool _showGrid = true;

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map,
        bool previewQuestHover = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        _canvas = canvas;
        _assets = assets;
        _map = map;
        _font = new PixelFont(canvas);
        _drawOrder = map.Tiles.OrderBy(tile => tile.Coordinate.R)
            .ThenBy(tile => tile.Coordinate.Q).ToArray();
        _pointsByCoordinate = map.PointsOfInterest.GroupBy(point => point.Coordinate)
            .ToDictionary(group => group.Key, group => group.First());
        if (previewQuestHover)
            _hover = map.PointsOfInterest.FirstOrDefault(point =>
                !point.Type.Equals("treasure", StringComparison.OrdinalIgnoreCase))?.Coordinate;
        _layout = WorldMapLayout.Fit(map, new Rectangle(25, 76, 1230, HudTop - 86));

        var pathMap = new BattleMap(_drawOrder.Select(tile =>
            new BattleTile(tile.Coordinate, tile.Height, tile.IsPassable)));
        _movement = new MovementSelection(pathMap,
            map.StartPosition ?? throw new ArgumentException("The map needs a starting position.", nameof(map)));
    }

    public void Update(MouseState mouse, KeyboardState keyboard)
    {
        _hover = mouse.Y < HudTop ? _layout.HitTest(mouse.Position, _drawOrder) : null;

        if (IsPressed(keyboard, _previousKeyboard, Keys.G))
            _showGrid = !_showGrid;
        if (IsPressed(keyboard, _previousKeyboard, Keys.E))
            InspectCurrentPointOfInterest();

        var clicked = mouse.LeftButton == ButtonState.Pressed &&
                      _previousMouse.LeftButton == ButtonState.Released;
        if (clicked && GridButton.Contains(mouse.Position))
        {
            _showGrid = !_showGrid;
        }
        else if (clicked && _hover is { } coordinate)
        {
            _interactionMessage = null;
            var moved = _movement.Select(coordinate);
            if (moved && _pointsByCoordinate.TryGetValue(coordinate, out var point))
                _interactionMessage = $"ВЫ ПОДОШЛИ К МЕСТУ: {point.Name}";
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    private static bool IsPressed(KeyboardState current, KeyboardState previous, Keys key) =>
        current.IsKeyDown(key) && !previous.IsKeyDown(key);

    private void InspectCurrentPointOfInterest()
    {
        _interactionMessage = _pointsByCoordinate.TryGetValue(_movement.Position, out var point)
            ? $"МЕСТО: {point.Name}"
            : null;
    }

    public void Draw()
    {
        FantasyArt.DrawLandscape(_canvas);
        DrawMapTiles();
        DrawElevationEdges();
        DrawTerrainProps();
        DrawPointsOfInterest();
        DrawPath();
        DrawHero();
        DrawHover();
        DrawTitle();
        DrawHud();
    }

    private void DrawMapTiles()
    {
        foreach (var tile in _drawOrder)
        {
            var corners = Corners(tile);
            _canvas.Polygon(corners, TerrainTint(tile.TerrainId));
            if (_showGrid)
                _canvas.Outline(corners, Translucent(197, 194, 143, 76), 1);
        }
    }

    private static Color TerrainTint(string terrainId) => terrainId switch
    {
        "forest" => Translucent(39, 67, 30, 62),
        "hill" => Translucent(181, 150, 72, 62),
        "rock" => Translucent(115, 116, 94, 95),
        "old-tree" => Translucent(44, 74, 31, 74),
        _ => Translucent(95, 127, 51, 38)
    };

    private void DrawElevationEdges()
    {
        foreach (var tile in _drawOrder)
        {
            var corners = Corners(tile);
            var neighbors = tile.Coordinate.GetNeighbors();
            for (var direction = 0; direction < neighbors.Count; direction++)
            {
                var neighbor = _map.GetTile(neighbors[direction]);
                if (neighbor is null || tile.Height <= neighbor.Height)
                    continue;

                var edge = SharedEdgeByNeighbor[direction];
                var first = corners[edge];
                var second = corners[(edge + 1) % corners.Count];
                var midpoint = (first + second) / 2;
                var center = Center(tile);
                var inward = Vector2.Normalize(center - midpoint);
                _canvas.Line(first, second, Translucent(45, 48, 29, 220),
                    2 + tile.Height - neighbor.Height);
                _canvas.Line(first + inward * 2, second + inward * 2,
                    Translucent(157, 162, 87, 190), 1);
            }
        }
    }

    private void DrawPointsOfInterest()
    {
        foreach (var point in _map.PointsOfInterest)
        {
            var tile = _map.GetTile(point.Coordinate);
            if (tile is null)
                continue;

            var center = Center(tile);
            var x = (int)center.X;
            var y = (int)center.Y;
            if (point.Type.Equals("treasure", StringComparison.OrdinalIgnoreCase))
            {
                if (!_assets.TryDraw(_canvas, AssetCatalog.Treasure,
                        new Rectangle(x - 23, y - 31, 46, 38)))
                    ExplorationArt.DrawTreasureChest(_canvas, x, y + 8);
                DrawTreasureMarker(x, y - 38);
            }
            else if (tile.TerrainId == "old-tree")
            {
                if (!_assets.TryDraw(_canvas, AssetCatalog.AncientOak,
                        new Rectangle(x - 58, y - 91, 116, 110)))
                    ExplorationArt.DrawAncientOak(_canvas, x, y + 9);
                DrawQuestMarker(x + 39, y - 83);
            }
            else
            {
                DrawQuestMarker(x, y - 34);
            }
        }
    }

    private void DrawTerrainProps()
    {
        foreach (var tile in _drawOrder.Where(tile => tile.TerrainId == "forest"))
        {
            var center = Center(tile);
            if (!_assets.TryDraw(_canvas, AssetCatalog.ForestTree,
                    new Rectangle((int)center.X - 33, (int)center.Y - 56, 66, 62)))
                ExplorationArt.DrawForestTree(_canvas, (int)center.X, (int)center.Y + 11);
        }

        foreach (var tile in _drawOrder.Where(tile => tile.TerrainId == "rock"))
        {
            var center = Center(tile);
            if (!_assets.TryDraw(_canvas, AssetCatalog.Rock,
                    new Rectangle((int)center.X - 30, (int)center.Y - 24, 60, 50)))
                FantasyArt.DrawRock(_canvas, (int)center.X, (int)center.Y);
        }
    }

    private void DrawQuestMarker(int x, int y)
    {
        FantasyArt.Oval(_canvas, x + 1, y + 11, 10, 4, Shadow);
        _canvas.Rect(x - 7, y - 9, 14, 19, Bronze);
        _canvas.Rect(x - 5, y - 7, 10, 15, new Color(223, 196, 128));
        _canvas.Rect(x - 1, y - 5, 2, 6, new Color(100, 59, 36));
        _canvas.Rect(x - 1, y + 3, 2, 2, new Color(100, 59, 36));
        _canvas.Rect(x - 9, y - 11, 18, 3, Highlight);
        _canvas.Rect(x - 9, y + 8, 18, 3, Bronze);
    }

    private void DrawTreasureMarker(int x, int y)
    {
        FantasyArt.Oval(_canvas, x + 1, y + 2, 12, 12, Shadow);
        FantasyArt.Oval(_canvas, x, y, 11, 11, Bronze);
        FantasyArt.Oval(_canvas, x, y - 1, 8, 8, Highlight);
        _canvas.Polygon([new(x, y - 5), new(x + 4, y), new(x, y + 5), new(x - 4, y)],
            new Color(117, 75, 35));
    }

    private void DrawPath()
    {
        var coordinates = _movement.Path.Coordinates;
        if (!_movement.Path.IsReachable || coordinates.Count < 2)
            return;

        for (var index = 1; index < coordinates.Count; index++)
        {
            var from = Center(_map.GetTile(coordinates[index - 1])!);
            var to = Center(_map.GetTile(coordinates[index])!);
            var distance = Vector2.Distance(from, to);
            var steps = Math.Max(1, (int)(distance / 15));
            for (var step = 1; step < steps; step += 2)
            {
                var point = Vector2.Lerp(from, to, step / (float)steps);
                FantasyArt.Oval(_canvas, (int)point.X + 1, (int)point.Y + 2, 4, 3, Shadow);
                FantasyArt.Oval(_canvas, (int)point.X, (int)point.Y, 3, 2, Highlight);
            }
        }

        if (_movement.Destination is { } destination && _map.GetTile(destination) is { } tile)
            _canvas.Outline(Corners(tile, 4), Highlight, 2);
    }

    private void DrawHero()
    {
        var center = Center(_map.GetTile(_movement.Position)!);
        FantasyArt.Oval(_canvas, (int)center.X, (int)center.Y + 18, 25, 8,
            Translucent(36, 45, 22, 170));
        DrawEllipseOutline((int)center.X, (int)center.Y + 18, 27, 9,
            Translucent(196, 193, 115, 205));
        if (!_assets.TryDraw(_canvas, AssetCatalog.FloraHero,
                new Rectangle((int)center.X - 25, (int)center.Y - 48, 50, 68)))
            FantasyArt.DrawHero(_canvas, (int)center.X, (int)center.Y);
    }

    private void DrawHover()
    {
        if (_hover is not { } coordinate || _map.GetTile(coordinate) is not { } tile)
            return;

        var color = _movement.Destination == coordinate && !_movement.Path.IsReachable
            ? new Color(183, 108, 75)
            : Translucent(226, 208, 139, 225);
        _canvas.Outline(Corners(tile, 3), color, 2);
    }

    private void DrawTitle()
    {
        _canvas.Rect(15, 14, 338, 44, Translucent(40, 34, 24, 208));
        _canvas.Rect(15, 14, 338, 2, Bronze);
        _canvas.Rect(15, 56, 338, 2, new Color(34, 30, 22));
        _font.Draw(_map.Name, 27, 27, Text, 2);

        _canvas.Rect(GridButton.X, GridButton.Y, GridButton.Width, GridButton.Height,
            Translucent(43, 37, 26, 220));
        _canvas.Frame(GridButton.X, GridButton.Y, GridButton.Width, GridButton.Height,
            _showGrid ? Highlight : Bronze, 2);
        _font.Draw(_showGrid ? "СЕТКА ВКЛ  G" : "СЕТКА ВЫКЛ  G",
            GridButton.X + 12, GridButton.Y + 16, _showGrid ? Text : Muted, 1);
    }

    private void DrawHud()
    {
        _canvas.Rect(0, HudTop, 1280, 120, new Color(46, 37, 26));
        _canvas.Rect(0, HudTop - 4, 1280, 3, Shadow);
        _canvas.Rect(0, HudTop - 1, 1280, 2, Bronze);
        _canvas.Rect(0, HudTop + 5, 1280, 1, new Color(35, 30, 22));
        _canvas.Rect(0, 715, 1280, 3, Bronze);
        _canvas.Rect(0, 718, 1280, 2, Shadow);

        _canvas.Rect(17, HudTop - 9, 102, 112, Shadow);
        _canvas.Frame(17, HudTop - 9, 102, 112, Bronze, 3);
        _canvas.Frame(22, HudTop - 4, 92, 102, Highlight * 0.6f, 1);
        var portrait = new Rectangle(26, HudTop + 1, 84, 94);
        if (!_assets.TryDraw(_canvas, AssetCatalog.FloraPortrait, portrait))
            FantasyArt.DrawPortrait(_canvas, portrait, true);

        _font.Draw("ГЕРОЙ", 143, HudTop + 19, Text, 2);
        var point = HoveredPointOfInterest();
        var headline = point?.Name.ToUpperInvariant() ?? "ПУТЕШЕСТВИЕ";
        _font.Draw(headline, 143, HudTop + 48, point is null ? Text : Highlight, 2);

        var hoveredTile = _hover is { } coordinate ? _map.GetTile(coordinate) : null;
        var detail = hoveredTile is null
            ? CurrentMovementText()
            : $"ВЫСОТА {hoveredTile.Height} / {TerrainName(hoveredTile.TerrainId)}";
        if (hoveredTile is not null)
        {
            var delta = hoveredTile.Height - _map.GetTile(_movement.Position)!.Height;
            if (delta != 0)
                detail += delta > 0 ? $" / ПОДЪЁМ +{delta}" : $" / СПУСК {delta}";
        }
        _font.Draw(detail, 143, HudTop + 79, Muted, 1);

        _font.Draw("ЛКМ - МАРШРУТ / ПОВТОРНО - ИДТИ", 618, HudTop + 38, Text, 2);
        var action = point is not null && point.Coordinate != _movement.Position
            ? point.Type.Equals("treasure", StringComparison.OrdinalIgnoreCase)
                ? "ЛКМ - МАРШРУТ К ТАЙНИКУ"
                : "ЛКМ - МАРШРУТ К ЗАДАНИЮ"
            : _pointsByCoordinate.ContainsKey(_movement.Position)
                ? "E - ОСМОТРЕТЬ"
                : "ESC - ВЫХОД";
        _font.Draw(action, 618, HudTop + 67, point is null ? Muted : Highlight, 1);
    }

    private string CurrentMovementText()
    {
        if (_interactionMessage is not null)
            return _interactionMessage;
        if (_movement.Destination is not null)
            return _movement.Path.IsReachable
                ? $"МАРШРУТ: {_movement.Path.Cost} КЛЕТОК"
                : "ПУТЬ ПЕРЕКРЫТ";
        return "ВЫБЕРИТЕ МЕСТО НА КАРТЕ";
    }

    private WorldPointOfInterest? HoveredPointOfInterest() =>
        _hover is { } coordinate && _pointsByCoordinate.TryGetValue(coordinate, out var point)
            ? point
            : null;

    private static string TerrainName(string terrainId) => terrainId switch
    {
        "forest" => "ЛЕС",
        "hill" => "ХОЛМ",
        "rock" => "СКАЛА",
        "old-tree" => "СТАРЫЙ ДУБ",
        _ => "ЛУГ"
    };

    private Vector2 Center(WorldTile tile)
    {
        var center = _layout.Center(tile);
        return new Vector2(center.X, center.Y);
    }

    private IReadOnlyList<Vector2> Corners(WorldTile tile, float inset = 0)
    {
        return _layout.Corners(tile, inset)
            .Select(point => new Vector2(point.X, point.Y)).ToArray();
    }

    private void DrawEllipseOutline(int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        for (var index = 0; index < 48; index++)
        {
            var firstAngle = MathHelper.TwoPi * index / 48;
            var secondAngle = MathHelper.TwoPi * (index + 1) / 48;
            _canvas.Line(new Vector2(centerX + MathF.Cos(firstAngle) * radiusX,
                    centerY + MathF.Sin(firstAngle) * radiusY),
                new Vector2(centerX + MathF.Cos(secondAngle) * radiusX,
                    centerY + MathF.Sin(secondAngle) * radiusY), color);
        }
    }

    private static Color Translucent(int red, int green, int blue, int alpha) =>
        new Color(red, green, blue) * (alpha / 255f);
}
