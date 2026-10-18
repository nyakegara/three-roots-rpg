using Game.Core.Combat;
using Game.Core.Combat.Pathfinding;
using Game.Core.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame;

/// <summary>
/// Visual/debug fixture only: these terrain and token placements do not define campaign
/// content or balance. Campaign maps will eventually come from JSON data.
/// </summary>
internal sealed class BattlePreviewScreen
{
    public static readonly Color Background = new(20, 29, 27);
    private static readonly Color Ink = new(13, 22, 20);
    private static readonly Color Panel = new(29, 41, 36);
    private static readonly Color Border = new(71, 87, 65);
    private static readonly Color Cream = new(239, 227, 186);
    private static readonly Color Muted = new(147, 163, 131);
    private static readonly Color Gold = new(226, 181, 85);
    private static readonly Color Mint = new(155, 205, 133);
    private static readonly Color Rust = new(195, 115, 76);
    private static readonly Color[] Ground =
    [new(63, 83, 57), new(75, 94, 62), new(89, 106, 67), new(106, 119, 73)];

    private readonly PixelCanvas _canvas;
    private readonly PixelFont _font;
    private readonly AssetCatalog _assets;
    private readonly HexLayout _layout = new();
    private readonly BattleMap _map;
    private readonly IReadOnlyList<BattleTile> _drawOrder;
    private readonly AStarPathfinder _pathfinder = new();
    private readonly HashSet<HexCoord> _occupied = [Offset(6, 3)];
    private HexCoord _start;
    private HexCoord _destination;
    private HexCoord? _hover;
    private PathResult _path = new(false, [], 0);
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;

    public BattlePreviewScreen(PixelCanvas canvas, AssetCatalog assets)
    {
        _canvas = canvas;
        _assets = assets;
        _font = new PixelFont(canvas);
        _map = CreateDemoMap();
        _drawOrder = _map.Tiles.OrderBy(t => t.Coordinate.R)
            .ThenBy(t => t.Coordinate.Q).ToArray();
        Reset();
        _hover = _destination;
    }

    public void Update(MouseState mouse, KeyboardState keyboard)
    {
        _hover = _layout.HitTest(mouse.Position, _drawOrder);
        if (keyboard.IsKeyDown(Keys.R) && !_previousKeyboard.IsKeyDown(Keys.R))
            Reset();

        if (_hover is { } coordinate)
        {
            if (mouse.LeftButton == ButtonState.Pressed &&
                _previousMouse.LeftButton == ButtonState.Released)
            {
                _destination = coordinate;
                Recalculate();
            }

            if (mouse.RightButton == ButtonState.Pressed &&
                _previousMouse.RightButton == ButtonState.Released &&
                _map.GetTile(coordinate) is { IsPassable: true } && !_occupied.Contains(coordinate))
            {
                _start = coordinate;
                Recalculate();
            }
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    private void Reset()
    {
        _start = Offset(1, 4);
        _destination = Offset(8, 2);
        Recalculate();
    }

    private void Recalculate() =>
        _path = _pathfinder.FindPath(_map, _start, _destination, _occupied);

    private static HexCoord Offset(int column, int row) => new(column - row / 2, row);

    private static BattleMap CreateDemoMap()
    {
        var rocks = new HashSet<HexCoord>
        {
            Offset(4, 1), Offset(4, 2), Offset(4, 4), Offset(4, 5),
            Offset(7, 4), Offset(7, 5), Offset(1, 0), Offset(8, 6)
        };
        var tiles = new List<BattleTile>();
        for (var row = 0; row < 7; row++)
        for (var col = 0; col < 10; col++)
        {
            var coordinate = Offset(col, row);
            var height = col >= 6 ? (row < 3 ? 3 : 2) : col >= 3 ? 1 : 0;
            tiles.Add(new BattleTile(coordinate, height, !rocks.Contains(coordinate)));
        }
        return new BattleMap(tiles);
    }

    public void Draw()
    {
        DrawFrame();
        foreach (var tile in _drawOrder)
            DrawTile(tile);

        DrawPath();
        foreach (var tile in _drawOrder)
        {
            var center = _layout.Center(tile);
            if (!tile.IsPassable)
                DrawRock((int)center.X, (int)center.Y);
            else if (!_path.Coordinates.Contains(tile.Coordinate) &&
                     !_occupied.Contains(tile.Coordinate) && tile.Coordinate != _start)
                DrawGrass(tile, (int)center.X, (int)center.Y);
            _font.Draw(tile.Height.ToString(), (int)center.X - 2, (int)center.Y + 22,
                _path.Coordinates.Contains(tile.Coordinate) ? Gold : Muted, 1);
        }

        foreach (var occupied in _occupied)
        {
            var center = _layout.Center(_map.GetTile(occupied)!);
            DrawEnemy((int)center.X, (int)center.Y);
        }

        var hero = _layout.Center(_map.GetTile(_start)!);
        DrawHero((int)hero.X, (int)hero.Y);
        if (_hover is { } hover)
            _canvas.Outline(_layout.Corners(_map.GetTile(hover)!, 5), Cream, 2);
        DrawPanel();
    }

    private void DrawFrame()
    {
        // Sparse background texture, deliberately fixed so screenshots are reproducible.
        for (var y = 0; y < 720; y += 8)
            _canvas.Rect(0, y, 1280, 1, new Color(23, 32, 29));

        _canvas.Rect(32, 28, 42, 47, Ink);
        _canvas.Frame(32, 28, 42, 47, Border);
        DrawSprout(53, 57, 2);
        _font.Draw("ТРИ КОРНЯ", 92, 30, Cream, 4);
        _font.Draw("ТАКТИЧЕСКИЙ ЭСКИЗ", 94, 70, Muted, 1);
        _font.Draw("ПОЛЕ БОЯ / ПОИСК ПУТИ", 408, 41, Cream, 2);
        _font.Draw("ИНТЕРАКТИВНЫЙ ПРОТОТИП", 409, 68, Muted, 1);
        _font.Draw("A* / HEX", 1132, 47, Gold, 2);
        _canvas.Rect(32, 100, 1216, 2, Border);

        _canvas.Rect(32, 122, 888, 510, Ink);
        _canvas.Frame(32, 122, 888, 510, Border);
        _font.Draw("01 / ТЕСТОВОЕ ПОЛЕ", 52, 140, Muted, 1);
        _font.Draw("10 X 7", 853, 140, Muted, 1);
        _font.Draw("ВЫСОТА: 0-3", 52, 619, Muted, 1);
        _font.Draw("ЧИСЛО НА КЛЕТКЕ = ВЫСОТА", 748, 619, Muted, 1);

        _canvas.Rect(32, 650, 1216, 42, Panel);
        _canvas.Frame(32, 650, 1216, 42, Border);
        KeyHint("ЛКМ", "ЦЕЛЬ", 48);
        KeyHint("ПКМ", "СТАРТ", 244);
        KeyHint("R", "СБРОС", 452);
        KeyHint("ESC", "ВЫХОД", 648);
        _font.Draw("НАВЕДЕНИЕ: ДАННЫЕ КЛЕТКИ", 938, 667, Muted, 1);
        _font.Draw("ДЕМОНСТРАЦИОННАЯ КАРТА / БЕЗ БОЕВОЙ ЛОГИКИ", 32, 704, Muted, 1);
        _font.Draw("MONOGAME", 1200, 704, Border, 1);
    }

    private void KeyHint(string key, string label, int x)
    {
        _canvas.Rect(x, 659, 42, 24, Ink);
        _canvas.Frame(x, 659, 42, 24, Border, 1);
        _font.Draw(key, x + (42 - key.Length * 6) / 2, 668, Gold, 1);
        _font.Draw(label, x + 54, 665, Cream, 2);
    }

    private void DrawTile(BattleTile tile)
    {
        var corners = _layout.Corners(tile);
        var depth = new Vector2(0, 7 + tile.Height * 5);
        // The three lower edges form the visible soil faces of a raised cell.
        for (var edge = 1; edge <= 3; edge++)
        {
            var next = edge + 1;
            Vector2[] face = [corners[edge], corners[next], corners[next] + depth, corners[edge] + depth];
            _canvas.Polygon(face, edge == 3 ? new Color(43, 54, 39) : new Color(51, 63, 44));
            _canvas.Outline(face, Ink, 2);
        }

        var onPath = _path.Coordinates.Contains(tile.Coordinate);
        var topColor = !tile.IsPassable ? new Color(64, 72, 58) :
            onPath ? new Color(112, 112, 61) : Ground[Math.Clamp(tile.Height, 0, 3)];
        _canvas.Polygon(corners, topColor);
        _canvas.Outline(corners, Ink, 2);
        _canvas.Outline(_layout.Corners(tile, 3), onPath ? new Color(168, 146, 73) : Border, 1);
    }

    private void DrawPath()
    {
        for (var index = 1; index < _path.Coordinates.Count; index++)
        {
            var from = _layout.Center(_map.GetTile(_path.Coordinates[index - 1])!);
            var to = _layout.Center(_map.GetTile(_path.Coordinates[index])!);
            _canvas.Line(from, to, Ink, 7);
            _canvas.Line(from, to, Gold, 3);
        }

        for (var index = 1; index < _path.Coordinates.Count - 1; index++)
        {
            var center = _layout.Center(_map.GetTile(_path.Coordinates[index])!);
            _canvas.Rect((int)center.X - 7, (int)center.Y - 7, 14, 14, Ink);
            _font.Draw(index.ToString(), (int)center.X - (index < 10 ? 2 : 5),
                (int)center.Y - 3, Cream, 1);
        }

        var destinationTile = _map.GetTile(_destination)!;
        var target = _layout.Center(destinationTile);
        var color = _path.IsReachable ? Gold : Rust;
        _canvas.Outline(_layout.Corners(destinationTile, 9), color, 2);
        _canvas.Frame((int)target.X - 9, (int)target.Y - 9, 19, 19, Ink, 4);
        _canvas.Frame((int)target.X - 8, (int)target.Y - 8, 17, 17, color, 2);
        _canvas.Rect((int)target.X - 2, (int)target.Y - 2, 5, 5, color);
    }

    private void DrawGrass(BattleTile tile, int x, int y)
    {
        var seed = Math.Abs(tile.Coordinate.Q * 17 + tile.Coordinate.R * 11);
        if (seed % 4 == 0)
        {
            DrawSprout(x - 8, y + 1, 1);
            _canvas.Rect(x + 12, y + 8, 3, 2, new Color(119, 139, 79));
        }
        else
        {
            var grass = new Color(108, 128, 77);
            _canvas.Rect(x - 13, y + 4, 2, 4, grass);
            _canvas.Rect(x - 10, y + 1, 2, 6, grass);
            _canvas.Rect(x + 15, y - 9, 3, 2, grass);
        }
    }

    private void DrawSprout(int x, int y, int scale)
    {
        _canvas.Rect(x - scale, y - 6 * scale, 2 * scale, 11 * scale, Mint);
        _canvas.Rect(x - 7 * scale, y - 10 * scale, 6 * scale, 4 * scale, new Color(94, 146, 90));
        _canvas.Rect(x - 5 * scale, y - 6 * scale, 5 * scale, 3 * scale, Mint);
        _canvas.Rect(x + scale, y - 14 * scale, 6 * scale, 5 * scale, Mint);
        _canvas.Rect(x + scale, y - 9 * scale, 4 * scale, 3 * scale, new Color(94, 146, 90));
    }

    private void DrawHero(int x, int y)
    {
        if (_assets.TryDraw(_canvas, AssetCatalog.FloraHero, new Rectangle(x - 24, y - 42, 48, 64)))
        {
            _canvas.Rect(x - 18, y + 23, 36, 3, Mint);
            return;
        }

        _canvas.Rect(x - 16, y + 12, 32, 8, Ink);
        _canvas.Rect(x - 10, y - 6, 20, 23, Ink);
        _canvas.Rect(x - 7, y - 4, 14, 17, new Color(107, 146, 83));
        _canvas.Rect(x - 5, y - 15, 12, 12, Ink);
        _canvas.Rect(x - 3, y - 13, 8, 9, Cream);
        _canvas.Rect(x + 2, y - 10, 2, 2, Ink);
        _canvas.Rect(x - 12, y + 13, 8, 6, new Color(146, 110, 67));
        _canvas.Rect(x + 3, y + 13, 9, 6, new Color(146, 110, 67));
        DrawSprout(x - 1, y - 17, 1);
        _canvas.Line(new Vector2(x + 16, y - 11), new Vector2(x + 16, y + 14), Gold, 3);
        _canvas.Rect(x + 12, y - 14, 9, 5, Mint);
        _canvas.Rect(x - 18, y + 23, 36, 3, Mint);
    }

    private void DrawEnemy(int x, int y)
    {
        if (_assets.TryDraw(_canvas, AssetCatalog.HumanGuard, new Rectangle(x - 24, y - 42, 48, 64)))
        {
            _canvas.Rect(x - 18, y + 23, 36, 3, Rust);
            return;
        }

        _canvas.Rect(x - 15, y + 11, 30, 8, Ink);
        _canvas.Rect(x - 12, y - 7, 24, 21, Ink);
        _canvas.Rect(x - 9, y - 4, 18, 16, Rust);
        _canvas.Rect(x - 8, y - 20, 16, 16, Ink);
        _canvas.Rect(x - 5, y - 17, 10, 10, new Color(204, 150, 100));
        _canvas.Rect(x - 3, y - 13, 2, 2, Ink);
        _canvas.Rect(x + 2, y - 13, 2, 2, Ink);
        _canvas.Rect(x - 16, y - 2, 5, 13, Rust);
        _canvas.Rect(x + 11, y - 2, 5, 13, Rust);
        _canvas.Rect(x - 18, y + 23, 36, 3, Rust);
    }

    private void DrawRock(int x, int y)
    {
        if (_assets.TryDraw(_canvas, AssetCatalog.Rock, new Rectangle(x - 28, y - 28, 56, 48)))
            return;

        Vector2[] shape = [new(x - 23, y + 10), new(x - 19, y - 7), new(x - 8, y - 20),
            new(x + 10, y - 18), new(x + 22, y - 1), new(x + 25, y + 12), new(x + 2, y + 19)];
        _canvas.Polygon(shape, new Color(108, 116, 96));
        _canvas.Outline(shape, Ink, 3);
        _canvas.Polygon([new(x - 8, y - 18), new(x + 8, y - 16), new(x + 19, y), new(x, y + 2)],
            new Color(139, 144, 115));
        _canvas.Line(new Vector2(x, y + 2), new Vector2(x + 2, y + 16), Border, 2);
        _canvas.Rect(x - 19, y + 8, 10, 4, new Color(77, 94, 60));
    }

    private void DrawPanel()
    {
        _canvas.Rect(944, 122, 304, 510, Panel);
        _canvas.Frame(944, 122, 304, 510, Border);
        _canvas.Rect(944, 122, 304, 4, Gold);
        _font.Draw("МАРШРУТ", 966, 146, Cream, 3);
        _font.Draw("ВЫБЕРИТЕ КЛЕТКУ НА ПОЛЕ", 966, 179, Muted, 1);
        _canvas.Rect(966, 202, 260, 1, Border);
        _font.Draw("СТАРТ", 966, 219, Muted, 2);
        _font.Draw(Coordinate(_start), 1100, 219, Mint, 2);
        _font.Draw("ЦЕЛЬ", 966, 252, Muted, 2);
        _font.Draw(Coordinate(_destination), 1100, 252, Gold, 2);

        _canvas.Rect(966, 286, 260, 98, Ink);
        _font.Draw("СТОИМОСТЬ ПУТИ", 980, 301, Muted, 1);
        _font.Draw(_path.IsReachable ? _path.Cost.ToString() : "--", 980, 324, Gold, 4);
        _font.Draw("ШАГОВ", 1092, 325, Muted, 1);
        _font.Draw(_path.IsReachable ? Math.Max(0, _path.Coordinates.Count - 1).ToString() : "--",
            1092, 343, Cream, 2);
        _font.Draw(_path.IsReachable ? "ПУТЬ НАЙДЕН" : "ПУТЬ НЕДОСТУПЕН", 966, 402,
            _path.IsReachable ? Mint : Rust, 2);

        Legend(966, 440, Mint, "ГЕРОЙ / СТАРТ");
        Legend(966, 462, Gold, "МАРШРУТ / ЦЕЛЬ");
        Legend(966, 484, Rust, "ЗАНЯТО / МАНЕКЕН");
        Legend(966, 506, new Color(139, 144, 115), "КАМНИ / НЕПРОХОДИМО");

        _canvas.Rect(966, 536, 260, 1, Border);
        _font.Draw("ПОД КУРСОРОМ", 966, 551, Muted, 1);
        if (_hover is { } hover)
        {
            var tile = _map.GetTile(hover)!;
            _font.Draw($"Q:{hover.Q}  R:{hover.R}  H:{tile.Height}", 966, 571, Cream, 2);
            _font.Draw(!tile.IsPassable ? "НЕПРОХОДИМАЯ КЛЕТКА" :
                _occupied.Contains(hover) ? "КЛЕТКА ЗАНЯТА" : "ПРОХОДИМАЯ КЛЕТКА", 966, 603,
                !tile.IsPassable || _occupied.Contains(hover) ? Rust : Mint, 1);
        }
        else
            _font.Draw("НАВЕДИТЕ НА КЛЕТКУ", 966, 578, Muted, 1);
    }

    private void Legend(int x, int y, Color color, string label)
    {
        _canvas.Rect(x, y - 1, 10, 10, Ink);
        _canvas.Rect(x + 2, y + 1, 6, 6, color);
        _font.Draw(label, x + 21, y, Muted, 1);
    }

    private static string Coordinate(HexCoord coordinate) => $"({coordinate.Q},{coordinate.R})";
}
