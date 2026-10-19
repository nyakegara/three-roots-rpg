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
    private readonly AssetCatalog _assets;
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
    private readonly Action _exit;
    private MouseState _currentMouse;
    private KeyboardState _currentKeyboard;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private HexCoord? _hover;
    private string? _interactionMessage;
    private RecruitmentScreen? _recruitmentScreen;
    private EncounterScreen? _encounterScreen;
    private BattleScreen? _battleScreen;

    public bool HasOpenRecruitmentScreen => _recruitmentScreen is not null;
    public bool HasOpenModal => _recruitmentScreen is not null || _encounterScreen is not null || _battleScreen is not null;

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map,
        bool previewQuestHover = false)
        : this(canvas, assets, map, new Hero("tutorial-hero", "Путник", 250), null, null, previewQuestHover)
    {
    }

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, CampaignDefinition definition,
        UnitCatalog units, bool previewQuestHover = false, string? previewRecruitmentSiteId = null,
        Action? exit = null, CampaignCaptureStage capture = CampaignCaptureStage.None)
        : this(canvas, assets, definition.Map, definition.CreateGameState(), units,
            previewQuestHover, previewRecruitmentSiteId, exit, capture)
    {
    }

    private ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map, Campaign campaign,
        UnitCatalog units, bool previewQuestHover, string? previewRecruitmentSiteId, Action? exit,
        CampaignCaptureStage capture)
        : this(canvas, assets, map, campaign.Hero, campaign, units, previewQuestHover, previewRecruitmentSiteId, exit, capture)
    {
    }

    private ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map, Hero hero,
        Campaign? campaign, UnitCatalog? units, bool previewQuestHover, string? previewRecruitmentSiteId = null,
        Action? exit = null, CampaignCaptureStage capture = CampaignCaptureStage.None)
    {
        ArgumentNullException.ThrowIfNull(map);
        _canvas = canvas;
        _assets = assets;
        _map = map;
        _hero = hero;
        _campaign = campaign;
        _units = units;
        _exit = exit ?? (() => { });
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
        _hud = new ExplorationHud(canvas, assets, units);
        var battleMap = new BattleMap(_tiles.Select(tile =>
            new BattleTile(tile.Coordinate, tile.Height, tile.IsPassable)));
        _movement = new MovementSelection(battleMap, start);
        if (previewQuestHover)
            _hover = map.PointsOfInterest.FirstOrDefault(point =>
                !point.Type.Equals("treasure", StringComparison.OrdinalIgnoreCase))?.Coordinate;
        if (previewRecruitmentSiteId is not null && campaign is not null && units is not null)
        {
            var recruitmentPoint = map.PointsOfInterest.FirstOrDefault(point =>
                point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase) &&
                point.RecruitmentSiteId == previewRecruitmentSiteId);
            if (recruitmentPoint is not null &&
                campaign.TryGetRecruitmentSiteAt(recruitmentPoint, recruitmentPoint.Coordinate, out var site) &&
                site is not null)
            {
                _movement = new MovementSelection(battleMap, recruitmentPoint.Coordinate);
                VisitPoint(recruitmentPoint);
            }
        }
        if (capture != CampaignCaptureStage.None && campaign is not null && units is not null)
        {
            var encounter = campaign.Encounters.First();
            var point = map.PointsOfInterest.Single(candidate => candidate.Id == encounter.PointOfInterestId);
            if (capture != CampaignCaptureStage.EmptyEncounter) CampaignCaptureSetup.RecruitParty(campaign);
            _movement = new MovementSelection(battleMap, point.Coordinate);
            var position = _projection.Project(point.Coordinate, map.GetTile(point.Coordinate)!.Height);
            _camera = new AdventureMapCamera(new NumericsVector2(1280, MapViewportHeight), _projection.WorldSize,
                new NumericsVector2(position.X - 640, position.Y - 300));
            _hover = point.Coordinate;
            VisitPoint(point);
            if (capture is CampaignCaptureStage.Combat or CampaignCaptureStage.Result or CampaignCaptureStage.Returned)
            {
                var session = StartEncounter(point);
                if (session is null) throw new InvalidOperationException("Cannot start campaign capture encounter.");
                if (capture is CampaignCaptureStage.Result or CampaignCaptureStage.Returned)
                {
                    BattleCaptureSetup.Advance(session.Battle, BattleCaptureStage.Result);
                    if (capture == CampaignCaptureStage.Returned && !ReturnFromBattle(session).Success)
                        throw new InvalidOperationException("Cannot resolve campaign capture encounter.");
                }
            }
        }
    }

    public void Update(MouseState mouse, KeyboardState keyboard, float elapsedSeconds)
    {
        _currentMouse = mouse;
        _currentKeyboard = keyboard;
        IGameScreen? modal = _battleScreen ?? (IGameScreen?)_encounterScreen ?? _recruitmentScreen;
        if (modal is not null)
        {
            modal.Update(mouse, keyboard, elapsedSeconds);
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
        if (_campaign is not null && _units is not null)
        {
            var encounter = _campaign.Encounters.FirstOrDefault(candidate => candidate.PointOfInterestId == point.Id);
            if (encounter is not null && !_campaign.CompletedEncounterIds.Contains(encounter.Id))
            {
                _encounterScreen = new EncounterScreen(_canvas, _assets, _units, _hero, encounter,
                    () => StartEncounter(point), () => _encounterScreen = null, _currentMouse, _currentKeyboard);
                return;
            }
            if (encounter is not null)
            {
                _interactionMessage = "ДОЗОР УЖЕ ПОБЕЖДЁН. ПУТЬ СВОБОДЕН.";
                return;
            }
        }
        if (point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase) &&
            _campaign is not null && _units is not null &&
            _campaign.TryGetRecruitmentSiteAt(point, _movement.Position, out var site) && site is not null)
        {
            _recruitmentScreen = new RecruitmentScreen(_canvas, _hero, site, _units, _assets, point.Name,
                () => _recruitmentScreen = null);
            return;
        }

        _interactionMessage = $"ВЫ ПОДОШЛИ К МЕСТУ: {point.Name}";
    }

    private CampaignBattle? StartEncounter(WorldPointOfInterest point)
    {
        var result = _campaign!.TryStartEncounter(point, _movement.Position, _units!);
        if (!result.Success || result.Session is null)
        {
            _encounterScreen?.ShowError();
            return null;
        }
        var session = result.Session;
        _encounterScreen = null;
        _battleScreen = new BattleScreen(_canvas, _assets, _units!, session,
            () => ReturnFromBattle(session), _exit, _currentMouse, _currentKeyboard);
        return session;
    }

    private EncounterResolution ReturnFromBattle(CampaignBattle session)
    {
        var result = _campaign!.ResolveEncounter(session);
        if (!result.Success) return result;
        _battleScreen = null;
        _interactionMessage = result.Victory
            ? $"ПОБЕДА: +{result.GoldAwarded} ЗОЛОТА, +{result.ExperienceAwarded} ОПЫТА. ПОТЕРИ СОХРАНЕНЫ."
            : "ПОРАЖЕНИЕ. ПОТЕРИ СОХРАНЕНЫ. НОВЫХ БОЙЦОВ МОЖНО НАНЯТЬ.";
        return result;
    }

    public void Draw()
    {
        if (_battleScreen is not null)
        {
            _battleScreen.Draw();
            return;
        }
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
        _encounterScreen?.Draw();
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
