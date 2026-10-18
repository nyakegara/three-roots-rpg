using Game.Core.Combat;
using Game.Core.Combat.Pathfinding;
using Game.Core.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame;

/// <summary>
/// Visual/debug fixture only. Placements and elevations do not define campaign content
/// or balance; eventual campaign maps will load from JSON.
/// </summary>
internal sealed class BattlePreviewScreen
{
    public static readonly Color Background = new(67, 89, 42);
    private static readonly Color Shadow = new(27, 24, 16);
    private static readonly Color Bronze = new(133, 107, 63);
    private static readonly Color Highlight = new(201, 172, 102);
    private static readonly Color Text = new(221, 211, 174);
    private static readonly Color Muted = new(164, 158, 129);
    private readonly PixelCanvas _canvas;
    private readonly PixelFont _font;
    private readonly AssetCatalog _assets;
    private readonly Action _exit;
    private readonly HexLayout _layout = new();
    private readonly BattleMap _map;
    private readonly IReadOnlyList<BattleTile> _drawOrder;
    private readonly HashSet<HexCoord> _occupied = [Offset(9, 3)];
    private MovementSelection _movement = null!;
    private HexCoord _start => _movement.Position;
    private HexCoord? _destination => _movement.Destination;
    private HexCoord? _hover;
    private PathResult _path => _movement.Path;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private Point _mousePosition;
    private bool _showGrid;
    private static readonly int[] SharedEdgeByNeighbor = [1, 0, 5, 4, 3, 2];

    public BattlePreviewScreen(PixelCanvas canvas, AssetCatalog assets, Action exit,
        bool showGrid = false)
    {
        _canvas = canvas;
        _assets = assets;
        _exit = exit;
        _font = new PixelFont(canvas);
        _map = CreateDemoMap();
        _drawOrder = _map.Tiles.OrderBy(t => t.Coordinate.R)
            .ThenBy(t => t.Coordinate.Q).ToArray();
        _showGrid = showGrid;
        _hover = showGrid ? Offset(8, 2) : null;
        Reset();
    }

    public void Update(MouseState mouse, KeyboardState keyboard)
    {
        _mousePosition = mouse.Position;
        _hover = mouse.Y < 600 ? _layout.HitTest(mouse.Position, _drawOrder) : null;
        if (keyboard.IsKeyDown(Keys.R) && !_previousKeyboard.IsKeyDown(Keys.R))
            Reset();
        if (keyboard.IsKeyDown(Keys.G) && !_previousKeyboard.IsKeyDown(Keys.G))
            _showGrid = !_showGrid;

        var clicked = mouse.LeftButton == ButtonState.Pressed &&
                      _previousMouse.LeftButton == ButtonState.Released;
        if (clicked && WithinButton(mouse.Position, 1080))
            _showGrid = !_showGrid;
        else if (clicked && WithinButton(mouse.Position, 1150))
            Reset();
        else if (clicked && WithinButton(mouse.Position, 1220))
            _exit();
        else if (clicked && _hover is { } coordinate)
        {
            _movement.Select(coordinate);
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    private static bool WithinButton(Point point, int centerX) =>
        (point.X - centerX) * (point.X - centerX) + (point.Y - 657) * (point.Y - 657) <= 24 * 24;

    private void Reset()
    {
        _movement = new MovementSelection(_map, Offset(2, 5), _occupied);
    }

    private static HexCoord Offset(int column, int row) => new(column - row / 2, row);

    private static BattleMap CreateDemoMap()
    {
        var rocks = new HashSet<HexCoord>
        {
            Offset(5, 2), Offset(5, 3), Offset(6, 4), Offset(8, 5),
            Offset(8, 6), Offset(3, 1), Offset(9, 1)
        };
        var tiles = new List<BattleTile>();
        for (var row = 0; row < 8; row++)
        for (var col = 0; col < 12; col++)
        {
            var coordinate = Offset(col, row);
            var height = col >= 7 && row <= 4 ? 2 : col >= 5 && row <= 5 ? 1 : 0;
            tiles.Add(new BattleTile(coordinate, height, !rocks.Contains(coordinate)));
        }
        return new BattleMap(tiles);
    }

    public void Draw()
    {
        FantasyArt.DrawLandscape(_canvas);

        if (_showGrid)
            DrawElevation();

        if (_showGrid)
            foreach (var tile in _drawOrder)
                _canvas.Outline(_layout.Corners(tile, 1), Translucent(188, 193, 133, 85), 1);

        DrawPath();
        if (_hover is { } hover && hover != _destination)
            _canvas.Outline(_layout.Corners(_map.GetTile(hover)!, 3),
                Translucent(221, 214, 157, 155), 1);

        // Paint rear-to-front so bodies stand naturally among the terrain props.
        foreach (var tile in _drawOrder)
        {
            var center = _layout.Center(tile);
            var x = (int)center.X;
            var y = (int)center.Y;
            if (!tile.IsPassable)
                DrawRock(x, y);
            else if (_occupied.Contains(tile.Coordinate))
                DrawGuard(x, y);
            else if (tile.Coordinate == _start)
                DrawHero(x, y);
        }

        DrawHud();
    }

    private void DrawPath()
    {
        for (var index = 1; index < _path.Coordinates.Count; index++)
        {
            var from = _layout.Center(_map.GetTile(_path.Coordinates[index - 1])!);
            var to = _layout.Center(_map.GetTile(_path.Coordinates[index])!);
            var direction = Vector2.Normalize(to - from);
            var normal = new Vector2(-direction.Y, direction.X);
            for (var step = 1; step <= 3; step++)
            {
                var point = Vector2.Lerp(from, to, step / 4f) + normal * (step % 2 == 0 ? 3 : -3);
                var foot = direction * 2.5f;
                _canvas.Line(point - foot + new Vector2(1, 1), point + foot + new Vector2(1, 1),
                    Translucent(57, 53, 28, 150), 3);
                _canvas.Line(point - foot, point + foot, Translucent(215, 209, 148, 205), 2);
            }
        }

        if (_destination is not { } destination || _map.GetTile(destination) is not { } destinationTile)
            return;
        var color = _path.IsReachable ? Highlight : new Color(181, 110, 76);
        _canvas.Outline(_layout.Corners(destinationTile, 4), color, 1);
        var target = _layout.Center(destinationTile);
        Ellipse((int)target.X, (int)target.Y + 3, 5, 2, color);
    }

    private void DrawElevation()
    {
        foreach (var tile in _drawOrder)
        {
            var tint = tile.Height switch
            {
                0 => Translucent(62, 91, 54, 48),
                1 => Translucent(162, 143, 74, 50),
                _ => Translucent(221, 180, 91, 61)
            };
            _canvas.Polygon(_layout.Corners(tile, 4), tint);
        }

        foreach (var tile in _drawOrder)
        {
            var corners = _layout.Corners(tile);
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
                var inward = Vector2.Normalize(_layout.Center(tile) - midpoint);

                // A dark earthen face with a thin grass rim marks only actual level changes.
                _canvas.Line(first, second, Translucent(45, 52, 33, 225), 3 + difference);
                _canvas.Line(first + inward * 2, second + inward * 2,
                    Translucent(143, 161, 88, 205), 1);
            }
        }
    }

    private void DrawHero(int x, int y)
    {
        Ellipse(x, y + 18, 24, 8, Translucent(39, 48, 23, 140));
        EllipseOutline(x, y + 18, 26, 9, Translucent(186, 192, 112, 190));
        if (!_assets.TryDraw(_canvas, AssetCatalog.FloraHero, new Rectangle(x - 24, y - 42, 48, 64)))
            FantasyArt.DrawHero(_canvas, x, y);
    }

    private void DrawGuard(int x, int y)
    {
        Ellipse(x, y + 18, 24, 8, Translucent(39, 48, 23, 140));
        if (!_assets.TryDraw(_canvas, AssetCatalog.HumanGuard, new Rectangle(x - 24, y - 42, 48, 64)))
            FantasyArt.DrawGuard(_canvas, x, y);
    }

    private void DrawRock(int x, int y)
    {
        if (!_assets.TryDraw(_canvas, AssetCatalog.Rock, new Rectangle(x - 28, y - 28, 56, 48)))
            FantasyArt.DrawRock(_canvas, x, y);
    }

    private void DrawHud()
    {
        // A narrow carved wood rail, with bronze bevels rather than framed data panels.
        _canvas.Rect(0, 610, 1280, 110, new Color(46, 37, 26));
        for (var y = 640; y < 712; y += 34)
            _canvas.Rect(0, y, 1280, 1, new Color(35, 30, 22));
        _canvas.Rect(0, 607, 1280, 3, Shadow);
        _canvas.Rect(0, 610, 1280, 2, Bronze);
        _canvas.Rect(0, 613, 1280, 1, Highlight * 0.6f);
        _canvas.Rect(0, 714, 1280, 3, Bronze);
        _canvas.Rect(0, 718, 1280, 2, Shadow);

        _canvas.Rect(17, 601, 102, 112, Shadow);
        _canvas.Frame(17, 601, 102, 112, Bronze, 3);
        _canvas.Frame(22, 606, 92, 102, Highlight * 0.6f, 1);
        var portraitBounds = new Rectangle(26, 610, 84, 94);
        if (!_assets.TryDraw(_canvas, AssetCatalog.FloraPortrait, portraitBounds))
            FantasyArt.DrawPortrait(_canvas, portraitBounds, true);
        foreach (var corner in new[] { new Point(20, 604), new Point(114, 604),
            new Point(20, 708), new Point(114, 708) })
            Ellipse(corner.X, corner.Y, 3, 3, Highlight);

        var header = _hover is { } hover && _map.GetTile(hover) is { } hoveredTile
            ? $"КЛЕТКА: ВЫСОТА {hoveredTile.Height}"
            : "ГЕРОЙ";
        _font.Draw(header, 143, 631, Text, 2);
        var message = _hover is { } h && _occupied.Contains(h) ? "ЭТА КЛЕТКА ЗАНЯТА" :
            _hover is { } cell && _map.GetTile(cell) is { IsPassable: false } ? "КАМНИ ПРЕГРАЖДАЮТ ПУТЬ" :
            _destination is null ? "ВЫБЕРИТЕ КЛЕТКУ" :
            !_path.IsReachable ? "СЮДА НЕ ПРОЙТИ" :
            _path.Cost == 0 ? "ГЕРОЙ УЖЕ ЗДЕСЬ" : $"ПУТЬ: {CellCount(_path.Cost)}";
        _font.Draw(message, 143, 665, Muted, 2);

        _font.Draw("ЛКМ - ПУТЬ / ЕЩЁ РАЗ - ИДТИ", 489, 651, Muted, 2);

        DrawRoundButton(1080, 0, "СЕТКА", _showGrid);
        DrawRoundButton(1150, 1, "СБРОС", false);
        DrawRoundButton(1220, 2, "ВЫХОД", false);
    }

    private void DrawRoundButton(int x, int icon, string label, bool active)
    {
        var hovered = WithinButton(_mousePosition, x);
        Ellipse(x + 1, 660, 26, 25, Shadow);
        Ellipse(x, 657, 25, 25, Bronze);
        Ellipse(x, 657, 22, 22, new Color(45, 44, 34));
        EllipseOutline(x, 657, 22, 22, hovered || active ? Highlight : new Color(98, 87, 57));
        var color = hovered || active ? Highlight : new Color(175, 164, 122);
        if (icon == 0)
        {
            for (var n = -1; n <= 1; n++)
            {
                _canvas.Line(new Vector2(x - 10, 657 + n * 7), new Vector2(x + 10, 657 + n * 7), color);
                _canvas.Line(new Vector2(x + n * 7, 647), new Vector2(x + n * 7, 667), color);
            }
        }
        else if (icon == 1)
        {
            for (var n = 0; n < 15; n++)
            {
                var a = MathHelper.ToRadians(-50 + n * 20);
                var b = MathHelper.ToRadians(-50 + (n + 1) * 20);
                _canvas.Line(new Vector2(x + MathF.Cos(a) * 10, 657 + MathF.Sin(a) * 10),
                    new Vector2(x + MathF.Cos(b) * 10, 657 + MathF.Sin(b) * 10), color, 2);
            }
            _canvas.Polygon([new(x - 13, 649), new(x - 4, 649), new(x - 8, 657)], color);
        }
        else
        {
            _canvas.Line(new Vector2(x - 7, 650), new Vector2(x + 7, 664), color, 2);
            _canvas.Line(new Vector2(x - 7, 664), new Vector2(x + 7, 650), color, 2);
        }

        _font.Draw(label, x - label.Length * 3, 693, Muted, 1);
    }

    private void Ellipse(int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        for (var y = -radiusY; y <= radiusY; y++)
        {
            var width = (int)(MathF.Sqrt(MathF.Max(0, 1 - y * y / (float)(radiusY * radiusY))) * radiusX);
            _canvas.Rect(centerX - width, centerY + y, width * 2 + 1, 1, color);
        }
    }

    private static Color Translucent(int red, int green, int blue, int alpha) =>
        new Color(red, green, blue) * (alpha / 255f);

    private static string CellCount(int count)
    {
        var noun = count % 100 is >= 11 and <= 14 ? "КЛЕТОК" : (count % 10) switch
        {
            1 => "КЛЕТКА",
            2 or 3 or 4 => "КЛЕТКИ",
            _ => "КЛЕТОК"
        };
        return $"{count} {noun}";
    }

    private void EllipseOutline(int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        for (var n = 0; n < 48; n++)
        {
            var a = MathHelper.TwoPi * n / 48;
            var b = MathHelper.TwoPi * (n + 1) / 48;
            _canvas.Line(new Vector2(centerX + MathF.Cos(a) * radiusX, centerY + MathF.Sin(a) * radiusY),
                new Vector2(centerX + MathF.Cos(b) * radiusX, centerY + MathF.Sin(b) * radiusY), color);
        }
    }
}
