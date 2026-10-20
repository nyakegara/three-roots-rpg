using Game.Core.Characters;
using Game.Core.Campaigns;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Geometry;
using Game.Core.Persistence;
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
    private readonly IReadOnlyDictionary<HexCoord, IReadOnlyList<WorldPointOfInterest>> _pointsByCoordinate;
    private readonly AdventureMapProjection _projection;
    private readonly AdventureMapCamera _camera;
    private readonly AdventureMapRenderer _renderer;
    private readonly ExplorationHud _hud;
    private readonly Hero _hero;
    private readonly Campaign? _campaign;
    private readonly UnitCatalog? _units;
    private MovementSelection _movement;
    private readonly Action _exit;
    private readonly Action<Campaign, HexCoord>? _saveProgress;
    private readonly string? _campaignId;
    private readonly bool _previewNorthGate;
    private MouseState _currentMouse;
    private KeyboardState _currentKeyboard;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private HexCoord? _hover;
    private string? _interactionMessage;
    private RecruitmentScreen? _recruitmentScreen;
    private EncounterScreen? _encounterScreen;
    private BattleScreen? _battleScreen;
    private QuestChoiceScreen? _questScreen;

    public bool HasOpenRecruitmentScreen => _recruitmentScreen is not null;
    public bool IsCampaignDefeated => _campaign?.IsDefeated == true;
    public bool IsCampaignVictory => _campaign?.IsVictory == true;
    public bool HasOpenModal => _recruitmentScreen is not null || _encounterScreen is not null ||
                                _battleScreen is not null || _questScreen is not null;

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map,
        bool previewQuestHover = false)
        : this(canvas, assets, map, new Hero("tutorial-hero", "Путник", 250), null, null, previewQuestHover)
    {
    }

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, CampaignDefinition definition,
        UnitCatalog units, bool previewQuestHover = false, string? previewRecruitmentSiteId = null,
        Action? exit = null, CampaignCaptureStage capture = CampaignCaptureStage.None,
        bool previewNorthGate = false, bool previewNorthGateOpen = false,
        CampaignSave? restoreSave = null, Action<Campaign, HexCoord>? saveProgress = null,
        string? startupMessage = null)
        : this(canvas, assets, definition.Map, definition.CreateGameState(), units,
            previewQuestHover, previewRecruitmentSiteId, exit, capture, previewNorthGate, previewNorthGateOpen,
            restoreSave, definition.Id, definition.PlayerFactionId, saveProgress, startupMessage)
    {
    }

    private ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map, Campaign campaign,
        UnitCatalog units, bool previewQuestHover, string? previewRecruitmentSiteId, Action? exit,
        CampaignCaptureStage capture, bool previewNorthGate = false, bool previewNorthGateOpen = false,
        CampaignSave? restoreSave = null, string? campaignId = null, string? playerFactionId = null,
        Action<Campaign, HexCoord>? saveProgress = null, string? startupMessage = null)
        : this(canvas, assets, map, campaign.Hero, campaign, units, previewQuestHover, previewRecruitmentSiteId,
            exit, capture, previewNorthGate, previewNorthGateOpen, restoreSave, campaignId, playerFactionId,
            saveProgress, startupMessage)
    {
    }

    private ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map, Hero hero,
        Campaign? campaign, UnitCatalog? units, bool previewQuestHover, string? previewRecruitmentSiteId = null,
        Action? exit = null, CampaignCaptureStage capture = CampaignCaptureStage.None,
        bool previewNorthGate = false, bool previewNorthGateOpen = false,
        CampaignSave? restoreSave = null, string? campaignId = null, string? playerFactionId = null,
        Action<Campaign, HexCoord>? saveProgress = null, string? startupMessage = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        _canvas = canvas;
        _assets = assets;
        _map = map;
        _hero = hero;
        _campaign = campaign;
        _units = units;
        _previewNorthGate = previewNorthGate;
        _exit = exit ?? (() => { });
        _saveProgress = saveProgress;
        _campaignId = campaignId;
        _font = new PixelFont(canvas);
        _tiles = map.Tiles.ToArray();
        _pointsByCoordinate = map.PointsOfInterest.GroupBy(point => point.Coordinate)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<WorldPointOfInterest>)group.ToArray());
        var floraMap = map.Id == "flora-01";
        _projection = new AdventureMapProjection(map, floraMap ? 60 : 112, floraMap ? 40 : 56,
            floraMap ? 5 : 10, floraMap ? 2 : 7);
        var start = map.StartPosition ?? throw new ArgumentException("The map needs a starting position.", nameof(map));
        string? restoreWarning = null;
        if (restoreSave is not null && campaign is not null && units is not null && campaignId is not null &&
            playerFactionId is not null && capture == CampaignCaptureStage.None && !previewNorthGate)
        {
            if (campaign.TryRestoreSave(restoreSave, campaignId, playerFactionId, map, units, out var error))
                start = restoreSave.HeroPosition;
            else
                restoreWarning = "СОХРАНЕНИЕ НЕ ЗАГРУЖЕНО: " + error.ToUpperInvariant();
        }
        var startTile = map.GetTile(start)!;
        var startPosition = _projection.Project(start, startTile.Height);
        _camera = new AdventureMapCamera(new NumericsVector2(1280, MapViewportHeight),
            _projection.WorldSize,
            new NumericsVector2(startPosition.X - 640, startPosition.Y - 300));
        _renderer = new AdventureMapRenderer(canvas, assets);
        _hud = new ExplorationHud(canvas, assets, units);
        _movement = new MovementSelection(CreateMovementMap(start), start);
        if (previewNorthGate && campaign is not null && floraMap)
        {
            if (previewNorthGateOpen)
            {
                campaign.ApplyChoice("pasture-choice", "help");
                campaign.TryRestoreProgress(new CampaignProgressSnapshot(["plants-pasture-battle"], false));
            }
            var gate = map.PointsOfInterest.Single(point => point.Id == "north-gate");
            var gatePosition = _projection.Project(gate.Coordinate, map.GetTile(gate.Coordinate)!.Height);
            _camera = new AdventureMapCamera(new NumericsVector2(1280, MapViewportHeight), _projection.WorldSize,
                new NumericsVector2(gatePosition.X - 640, gatePosition.Y - 300));
        }
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
                _movement = new MovementSelection(CreateMovementMap(recruitmentPoint.Coordinate), recruitmentPoint.Coordinate);
                VisitPoint(recruitmentPoint);
            }
        }
        if (capture != CampaignCaptureStage.None && campaign is not null && units is not null)
        {
            var encounter = campaign.Encounters.First(candidate => campaign.CanAccessEncounter(candidate.Id));
            var point = map.PointsOfInterest.Single(candidate => candidate.Id == encounter.PointOfInterestId);
            if (capture != CampaignCaptureStage.EmptyEncounter) CampaignCaptureSetup.RecruitParty(campaign);
            _movement = new MovementSelection(CreateMovementMap(point.Coordinate), point.Coordinate);
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
        else if (!previewNorthGate && restoreSave is null && startupMessage is null && floraMap && campaign is not null && units is not null &&
                 _pointsByCoordinate.Values.SelectMany(points => points).FirstOrDefault(point => point.Id == "guide-dialogue") is { } guide)
        {
            _movement = new MovementSelection(CreateMovementMap(start), start);
            VisitPoint(guide);
        }
        if (restoreWarning is not null) _interactionMessage = restoreWarning;
        if (startupMessage is not null) _interactionMessage = startupMessage;
        if (restoreSave is null && campaign is not null && _saveProgress is not null && campaignId is not null)
            SaveProgress();
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
        if (_questScreen is not null)
        {
            _questScreen.Update(mouse, keyboard);
            _previousMouse = mouse;
            _previousKeyboard = keyboard;
            return;
        }

        if (_campaign?.IsDefeated == true || _campaign?.IsVictory == true)
        {
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
            if (target == _movement.Position && _pointsByCoordinate.ContainsKey(target))
                VisitPointAt(target);
            else if (_movement.Select(target))
            {
                SaveProgress();
                if (_pointsByCoordinate.ContainsKey(target)) VisitPointAt(target);
            }
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    private static bool IsPressed(KeyboardState current, KeyboardState previous, Keys key) =>
        current.IsKeyDown(key) && !previous.IsKeyDown(key);

    private void InspectCurrentPointOfInterest()
    {
        if (_pointsByCoordinate.ContainsKey(_movement.Position))
            VisitPointAt(_movement.Position);
        else
            _interactionMessage = null;
    }

    private void VisitPoint(WorldPointOfInterest point)
    {
        if (_campaign is not null && _units is not null)
        {
            if (point.Type.Equals("quest", StringComparison.OrdinalIgnoreCase))
            {
                var quest = _campaign.Quests.FirstOrDefault(candidate => candidate.PointOfInterestId == point.Id);
                if (quest is not null && !_campaign.CompletedQuestIds.Contains(quest.Id))
                {
                    _questScreen = new QuestChoiceScreen(_canvas, quest, choiceId => ResolveQuestChoice(quest, choiceId),
                        () => _questScreen = null, _currentMouse, _currentKeyboard);
                    return;
                }
            }
            var encounter = _campaign.Encounters.FirstOrDefault(candidate => candidate.PointOfInterestId == point.Id);
            if (encounter is not null && _campaign.CompletedEncounterIds.Contains(encounter.Id))
            {
                _interactionMessage = "ДОЗОР УЖЕ ПОБЕЖДЁН. ПУТЬ СВОБОДЕН.";
                return;
            }
            if (encounter is not null && _campaign.CanAccessEncounter(encounter.Id))
            {
                _encounterScreen = new EncounterScreen(_canvas, _assets, _units, _hero, encounter,
                    () => StartEncounter(point), () => _encounterScreen = null, _currentMouse, _currentKeyboard);
                return;
            }
            if (encounter is not null && !_campaign.CanAccessEncounter(encounter.Id))
            {
                _interactionMessage = "ПРОХОД ПОКА ЗАКРЫТ. НУЖНО ВЫПОЛНИТЬ УСЛОВИЕ КАМПАНИИ.";
                return;
            }
        }
        if (point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase) &&
            _campaign is not null && _units is not null &&
            _campaign.TryGetRecruitmentSiteAt(point, _movement.Position, out var site) && site is not null)
        {
            _recruitmentScreen = new RecruitmentScreen(_canvas, _hero, site, _units, _assets, point.Name,
                () => _recruitmentScreen = null, SaveProgress);
            return;
        }

        _interactionMessage = $"ВЫ ПОДОШЛИ К МЕСТУ: {point.Name}";
    }

    private void VisitPointAt(HexCoord coordinate)
    {
        if (!_pointsByCoordinate.TryGetValue(coordinate, out var points)) return;
        var quest = points.FirstOrDefault(point => point.Type.Equals("quest", StringComparison.OrdinalIgnoreCase) &&
            _campaign?.Quests.Any(candidate => candidate.PointOfInterestId == point.Id &&
                !_campaign.CompletedQuestIds.Contains(candidate.Id)) == true);
        if (quest is not null) { VisitPoint(quest); return; }
        var encounterPoint = _campaign?.FindEncounterPointAt(_map, coordinate);
        if (encounterPoint is not null) { VisitPoint(encounterPoint); return; }
        var recruitment = points.FirstOrDefault(point => point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase));
        if (recruitment is not null) { VisitPoint(recruitment); return; }
        VisitPoint(points[0]);
    }

    private void ResolveQuestChoice(Game.Core.Campaigns.Quest quest, string choiceId)
    {
        var result = _campaign!.ApplyChoice(quest.Id, choiceId);
        _questScreen = null;
        if (!result.Success)
        {
            _interactionMessage = result.Message;
            return;
        }
        SaveProgress();
        var branchBattle = _campaign.Encounters.FirstOrDefault(encounter =>
            _campaign.CanAccessEncounter(encounter.Id) &&
            _map.PointsOfInterest.Any(point => point.Id == encounter.PointOfInterestId &&
                                               point.Coordinate == _movement.Position));
        if (branchBattle is not null)
        {
            var point = _map.PointsOfInterest.Single(candidate => candidate.Id == branchBattle.PointOfInterestId);
            VisitPoint(point);
        }
        else if (quest.Id == "guide-introduction")
            _interactionMessage = "КРЕСТЬЯНСКОЕ ПАСТБИЩЕ — К ЮГУ ПО ТРОПЕ.";
        else
            _interactionMessage = "РЕШЕНИЕ ПРИНЯТО.";
    }

    private BattleMap CreateMovementMap(HexCoord currentPosition) => _campaign?.CreateTravelMap(_map, currentPosition) ??
        new BattleMap(_tiles.Select(tile => new BattleTile(tile.Coordinate, tile.Height, tile.IsPassable)));

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
        _movement = new MovementSelection(CreateMovementMap(_movement.Position), _movement.Position);
        _interactionMessage = result.Victory
            ? $"ПОБЕДА: +{result.GoldAwarded} ЗОЛОТА, +{result.ExperienceAwarded} ОПЫТА. ПОТЕРИ СОХРАНЕНЫ."
            : _campaign.IsDefeated
                ? "ПОРАЖЕНИЕ. АРМИЯ УНИЧТОЖЕНА. КАМПАНИЯ ЗАВЕРШЕНА."
                : "ПОРАЖЕНИЕ. ПОТЕРИ СОХРАНЕНЫ. МОЖНО ВЕРНУТЬСЯ К ПОСЕЛЕНИЮ И ПОПОЛНИТЬ ОТРЯД.";
        SaveProgress();
        return result;
    }

    private void SaveProgress()
    {
        if (_campaign is not null) _saveProgress?.Invoke(_campaign, _movement.Position);
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
            _movement.Path.IsReachable ? _movement.Path.Coordinates : Array.Empty<HexCoord>(), _hover,
            _campaign?.IsUnlocked("north-gate") == true, drawHero: !_previewNorthGate);
        DrawMapPlaque();
        var hoveredPoint = _hover is { } coordinate && _pointsByCoordinate.TryGetValue(coordinate, out var points)
            ? points.FirstOrDefault() : null;
        _hud.Draw(_hero, _hero.Army.Stacks, hoveredPoint, CurrentActionText(hoveredPoint));
        if (_campaign?.IsDefeated == true || _campaign?.IsVictory == true) DrawCampaignEndingPanel();
        _encounterScreen?.Draw();
        _questScreen?.Draw();
    }

    private void DrawCampaignEndingPanel()
    {
        const int x = 306;
        const int y = 250;
        const int width = 668;
        const int height = 172;
        _canvas.Rect(x, y, width, height, new Color(40, 29, 21));
        _canvas.Frame(x, y, width, height, Bronze, 4);
        _canvas.Rect(x + 10, y + 10, width - 20, height - 20, new Color(218, 202, 157));
        _canvas.Frame(x + 10, y + 10, width - 20, height - 20, new Color(83, 57, 34), 2);
        var victory = _campaign?.IsVictory == true;
        _font.Draw(victory ? "ОСТРОВ СПАСЁН" : "КАМПАНИЯ ЗАВЕРШЕНА", x + 72, y + 35,
            new Color(58, 43, 28), 3);
        _font.Draw(victory ? "ПРЕДВОДИТЕЛЬ ЛЮДЕЙ ПОБЕЖДЁН. ФЛОРА ЗАЩИТИЛА ОСТРОВ."
                : "АРМИЯ ЛИАРЫ ПАЛА. ОСТРОВ ОСТАЛСЯ БЕЗ ЗАЩИТЫ.", x + 42, y + 88,
            new Color(76, 59, 37), 1);
        _font.Draw("R — НАЧАТЬ КАМПАНИЮ ЗАНОВО     ESC — ВЫХОД", x + 52, y + 126,
            new Color(76, 59, 37), 1);
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
