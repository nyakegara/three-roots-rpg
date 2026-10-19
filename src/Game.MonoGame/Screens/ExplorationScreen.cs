using Game.Core.Characters;
using Game.Core.Campaigns;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Geometry;
using Game.Core.World;
using Game.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NumericsVector2 = System.Numerics.Vector2;

namespace Game.MonoGame.Screens;

/// <summary>Scrollable campaign view with two-click travel and a fixed fantasy HUD.</summary>
internal sealed class ExplorationScreen : IGameScreen
{
    public static readonly Color Background = new(63, 76, 44);
    private const int MapViewportHeight = 596;
    private static readonly Color Bronze = new(141, 107, 59);

    private readonly PixelCanvas _canvas;
    private readonly PixelFont _font;
    private readonly WorldMap _map;
    private readonly IReadOnlyList<WorldTile> _tiles;
    private readonly IReadOnlyDictionary<HexCoord, WorldPointOfInterest> _pointsByCoordinate;
    private readonly AdventureMapProjection _projection;
    private readonly AdventureMapCamera _camera;
    private readonly AdventureMapRenderer _renderer;
    private readonly ExplorationHud _hud;
    private readonly Hero _hero;
    private readonly Campaign? _campaign;
    private readonly UnitCatalog? _units;
    private readonly MovementSelection _movement;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private HexCoord? _hover;
    private string? _interactionMessage;
    private RecruitmentScreen? _recruitmentScreen;

    public bool HasOpenRecruitmentScreen => _recruitmentScreen is not null;

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map,
        bool previewQuestHover = false)
        : this(canvas, assets, map, new Hero("tutorial-hero", "Путник", 250), null, null, previewQuestHover)
    {
    }

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, CampaignDefinition definition,
        UnitCatalog units, bool previewQuestHover = false, bool previewRecruitment = false)
        : this(canvas, assets, definition.Map, definition.CreateGameState(), units,
            previewQuestHover, previewRecruitment)
    {
    }

    private ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map, Campaign campaign,
        UnitCatalog units, bool previewQuestHover, bool previewRecruitment)
        : this(canvas, assets, map, campaign.Hero, campaign, units, previewQuestHover, previewRecruitment)
    {
    }

    private ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map, Hero hero,
        Campaign? campaign, UnitCatalog? units, bool previewQuestHover, bool previewRecruitment = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        _canvas = canvas;
        _map = map;
        _hero = hero;
        _campaign = campaign;
        _units = units;
        _font = new PixelFont(canvas);
        _tiles = map.Tiles.ToArray();
        _pointsByCoordinate = map.PointsOfInterest.GroupBy(point => point.Coordinate)
            .ToDictionary(group => group.Key, group => group.First());
        _projection = new AdventureMapProjection(map, 112, 56, 10);
        var start = map.StartPosition ?? throw new ArgumentException("The map needs a starting position.", nameof(map));
        var startTile = map.GetTile(start)!;
        var startPosition = _projection.Project(start, startTile.Height);
        _camera = new AdventureMapCamera(new NumericsVector2(1280, MapViewportHeight),
            _projection.WorldSize,
            new NumericsVector2(startPosition.X - 640, startPosition.Y - 300));
        _renderer = new AdventureMapRenderer(canvas, assets);
        _hud = new ExplorationHud(canvas, assets);
        var battleMap = new BattleMap(_tiles.Select(tile =>
            new BattleTile(tile.Coordinate, tile.Height, tile.IsPassable)));
        _movement = new MovementSelection(battleMap, start);
        if (previewQuestHover)
            _hover = map.PointsOfInterest.FirstOrDefault(point =>
                !point.Type.Equals("treasure", StringComparison.OrdinalIgnoreCase))?.Coordinate;
        if (previewRecruitment && campaign is not null && units is not null)
        {
            var recruitmentPoint = map.PointsOfInterest.FirstOrDefault(point =>
                point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase));
            if (recruitmentPoint is not null &&
                campaign.TryGetRecruitmentSiteAt(recruitmentPoint, recruitmentPoint.Coordinate, out var site) &&
                site is not null)
            {
                _movement = new MovementSelection(battleMap, recruitmentPoint.Coordinate);
                VisitPoint(recruitmentPoint);
            }
        }
    }

    public void Update(MouseState mouse, KeyboardState keyboard, float elapsedSeconds)
    {
        if (_recruitmentScreen is not null)
        {
            _recruitmentScreen.Update(mouse, keyboard, elapsedSeconds);
            _previousMouse = mouse;
            _previousKeyboard = keyboard;
            return;
        }

        var pointer = new NumericsVector2(mouse.X, mouse.Y);
        _camera.Update(pointer, elapsedSeconds);
        if (mouse.Y < MapViewportHeight && mouse.X >= 0 && mouse.X < 1280)
        {
            _hover = _projection.TryPick(_camera.ToWorld(pointer), out var coordinate) ? coordinate : null;
        }
        else _hover = null;

        if (IsPressed(keyboard, _previousKeyboard, Keys.E)) InspectCurrentPointOfInterest();
        var clicked = mouse.LeftButton == ButtonState.Pressed &&
                      _previousMouse.LeftButton == ButtonState.Released;
        if (clicked && mouse.Y < MapViewportHeight && _hover is { } target)
        {
            _interactionMessage = null;
            if (target == _movement.Position && _pointsByCoordinate.TryGetValue(target, out var currentPoint))
                VisitPoint(currentPoint);
            else if (_movement.Select(target) && _pointsByCoordinate.TryGetValue(target, out var reachedPoint))
                VisitPoint(reachedPoint);
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    private static bool IsPressed(KeyboardState current, KeyboardState previous, Keys key) =>
        current.IsKeyDown(key) && !previous.IsKeyDown(key);

    private void InspectCurrentPointOfInterest()
    {
        if (_pointsByCoordinate.TryGetValue(_movement.Position, out var point))
            VisitPoint(point);
        else
            _interactionMessage = null;
    }

    private void VisitPoint(WorldPointOfInterest point)
    {
        if (point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase) &&
            _campaign is not null && _units is not null &&
            _campaign.TryGetRecruitmentSiteAt(point, _movement.Position, out var site) && site is not null)
        {
            _recruitmentScreen = new RecruitmentScreen(_canvas, _hero, site, _units, point.Name,
                () => _recruitmentScreen = null);
            return;
        }

        _interactionMessage = $"ВЫ ПОДОШЛИ К МЕСТУ: {point.Name}";
    }

    public void Draw()
    {
        if (_recruitmentScreen is not null)
        {
            _recruitmentScreen.Draw();
            return;
        }

        _renderer.Draw(_map, _projection, _camera, _movement.Position,
            _movement.Path.IsReachable ? _movement.Path.Coordinates : Array.Empty<HexCoord>(), _hover);
        DrawMapPlaque();
        var hoveredPoint = _hover is { } coordinate && _pointsByCoordinate.TryGetValue(coordinate, out var point)
            ? point : null;
        _hud.Draw(_hero, _hero.Army.Stacks, hoveredPoint, CurrentActionText(hoveredPoint));
    }

    private void DrawMapPlaque()
    {
        _canvas.Rect(18, 17, 348, 50, new Color(46, 35, 24) * 0.83f);
        _canvas.Rect(18, 17, 348, 2, Bronze);
        _canvas.Rect(18, 65, 348, 2, new Color(43, 34, 24));
        _font.Draw(_map.Name, 31, 27, new Color(231, 213, 164), 2);
        _font.Draw("ПУТЬ ПРОЛЕГАЕТ СКВОЗЬ ТРИ КОРНЯ", 31, 49, new Color(187, 174, 137), 1);
    }

    private string CurrentActionText(WorldPointOfInterest? hoveredPoint)
    {
        if (_interactionMessage is not null) return _interactionMessage;
        if (hoveredPoint is not null && hoveredPoint.Coordinate != _movement.Position)
            return "ЛКМ — ПУТЬ  /  ЕЩЁ РАЗ — ИДТИ";
        if (_movement.Destination is not null)
            return _movement.Path.IsReachable ? $"МАРШРУТ: {_movement.Path.Cost} ШАГОВ" : "ПУТЬ ПЕРЕКРЫТ";
        return _pointsByCoordinate.ContainsKey(_movement.Position) ? "E — ОСМОТРЕТЬ МЕСТО" : "ЛКМ — ВЫБРАТЬ ЦЕЛЬ";
    }
}
