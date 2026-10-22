using Game.Core.Characters;
using Game.Core.Campaigns;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Geometry;
using Game.Core.Persistence;
using Game.Core.World;
using Game.Core.Navigation;
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
    private readonly Action<Campaign, HexCoord, MapPosition?>? _saveProgress;
    private AdventureTravel? _travel;
    private AdventureSurface? _surface;
    private MapPosition? _hoverPosition;
    private WorldPointOfInterest? _hoverPoint;
    private WorldPointOfInterest? _travelPoint;
    private float _travelSaveSeconds;
    private readonly string? _campaignId;
    private readonly bool _previewNorthTrail;
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
    private readonly ExplorationPanels _panels;
    private ExplorationPanelKind _openPanel;
    private float _idleSeconds;
    public bool ShowNavigation { get; set; }

    public bool HasOpenRecruitmentScreen => _recruitmentScreen is not null;
    public bool IsCampaignDefeated => _campaign?.IsDefeated == true;
    public bool IsCampaignVictory => _campaign?.IsVictory == true;
    public bool HasOpenModal => _recruitmentScreen is not null || _encounterScreen is not null ||
                                _battleScreen is not null || _questScreen is not null ||
                                _openPanel != ExplorationPanelKind.None;

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map,
        bool previewQuestHover = false)
        : this(canvas, assets, map, new Hero("tutorial-hero", "Путник", 250), null, null, previewQuestHover)
    {
    }

    public ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, CampaignDefinition definition,
        UnitCatalog units, bool previewQuestHover = false, string? previewRecruitmentSiteId = null,
        Action? exit = null, CampaignCaptureStage capture = CampaignCaptureStage.None,
        bool previewNorthTrail = false,
        bool previewIdleMap = false,
        bool previewMapPanel = false, bool previewJournalPanel = false,
        CampaignSave? restoreSave = null, Action<Campaign, HexCoord, MapPosition?>? saveProgress = null,
        string? startupMessage = null)
        : this(canvas, assets, definition.Map, definition.CreateGameState(), units,
            previewQuestHover, previewRecruitmentSiteId, exit, capture, previewNorthTrail, previewIdleMap,
            previewMapPanel, previewJournalPanel,
            restoreSave, definition.Id, definition.PlayerFactionId, saveProgress, startupMessage)
    {
    }

    private ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map, Campaign campaign,
        UnitCatalog units, bool previewQuestHover, string? previewRecruitmentSiteId, Action? exit,
        CampaignCaptureStage capture, bool previewNorthTrail = false,
        bool previewIdleMap = false,
        bool previewMapPanel = false, bool previewJournalPanel = false,
        CampaignSave? restoreSave = null, string? campaignId = null, string? playerFactionId = null,
        Action<Campaign, HexCoord, MapPosition?>? saveProgress = null, string? startupMessage = null)
        : this(canvas, assets, map, campaign.Hero, campaign, units, previewQuestHover, previewRecruitmentSiteId,
            exit, capture, previewNorthTrail, previewIdleMap, previewMapPanel, previewJournalPanel,
            restoreSave, campaignId, playerFactionId,
            saveProgress, startupMessage)
    {
    }

    private ExplorationScreen(PixelCanvas canvas, AssetCatalog assets, WorldMap map, Hero hero,
        Campaign? campaign, UnitCatalog? units, bool previewQuestHover, string? previewRecruitmentSiteId = null,
        Action? exit = null, CampaignCaptureStage capture = CampaignCaptureStage.None,
        bool previewNorthTrail = false,
        bool previewIdleMap = false,
        bool previewMapPanel = false, bool previewJournalPanel = false,
        CampaignSave? restoreSave = null, string? campaignId = null, string? playerFactionId = null,
        Action<Campaign, HexCoord, MapPosition?>? saveProgress = null, string? startupMessage = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        _canvas = canvas;
        _assets = assets;
        _map = map;
        _hero = hero;
        _campaign = campaign;
        _units = units;
        _previewNorthTrail = previewNorthTrail;
        _exit = exit ?? (() => { });
        _saveProgress = saveProgress;
        _campaignId = campaignId;
        _font = new PixelFont(canvas);
        _tiles = map.Tiles.ToArray();
        _pointsByCoordinate = map.PointsOfInterest.GroupBy(point => point.Coordinate)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<WorldPointOfInterest>)group.ToArray());
        var floraMap = map.Id == "flora-01";
        // Flora's commissioned island is a wide 2.2:1 map. Widen the horizontal
        // projection to preserve its coastline and biome proportions at runtime.
        _projection = new AdventureMapProjection(map, floraMap ? 88 : 112, floraMap ? 40 : 56,
            floraMap ? 5 : 10, floraMap ? 2 : 7);
        var start = map.StartPosition ?? throw new ArgumentException("The map needs a starting position.", nameof(map));
        string? restoreWarning = null;
        if (restoreSave is not null && campaign is not null && units is not null && campaignId is not null &&
            playerFactionId is not null && capture == CampaignCaptureStage.None && !previewNorthTrail)
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
        _panels = new ExplorationPanels(canvas, assets);
        _openPanel = previewMapPanel ? ExplorationPanelKind.Map :
            previewJournalPanel ? ExplorationPanelKind.Journal : ExplorationPanelKind.None;
        _movement = new MovementSelection(CreateMovementMap(start), start);
        if (previewNorthTrail && campaign is not null && floraMap)
        {
            var trail = map.PointsOfInterest.Single(point => point.Id == "north-gate");
            var gatePosition = _projection.Project(trail.Coordinate, map.GetTile(trail.Coordinate)!.Height);
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
        if (capture == CampaignCaptureStage.AfterChoice && campaign is not null)
        {
            if (!campaign.ApplyChoice("guide-introduction", "begin").Success ||
                !campaign.ApplyChoice("pasture-choice", "help").Success)
                throw new InvalidOperationException("Cannot prepare the completed-choice capture.");
            var point = map.PointsOfInterest.Single(point => point.Id == "pasture-request");
            _movement = new MovementSelection(CreateMovementMap(point.Coordinate), point.Coordinate);
        }
        else if (capture != CampaignCaptureStage.None && campaign is not null && units is not null)
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
        else if (!previewNorthTrail && !previewIdleMap && !previewMapPanel && !previewJournalPanel &&
                 restoreSave is null && startupMessage is null && floraMap && campaign is not null && units is not null &&
                 _pointsByCoordinate.Values.SelectMany(points => points).FirstOrDefault(point => point.Id == "guide-dialogue") is { } guide)
        {
            _movement = new MovementSelection(CreateMovementMap(start), start);
            VisitPoint(guide);
        }
        if (map.Navigation is { } navigation)
        {
            _surface = new(navigation, campaign is not null ? campaign.IsUnlocked : null);
            var initial = restoreWarning is null && restoreSave?.AdventurePosition is { } saved
                ? saved : _projection.ToArt(_projection.Project(_movement.Position, map.GetTile(_movement.Position)!.Height));
            initial = new AdventurePathfinder(_surface).FindNearestWalkable(initial);
            _travel = new(_surface, initial);
            if (capture == CampaignCaptureStage.AfterChoice)
            {
                _travel = new(_surface, navigation.Start);
                var landmark = InteractionPosition(map.PointsOfInterest.Single(point => point.Id == "pasture-request"));
                _travel.SelectApproach(landmark);
                if (!_travel.SelectApproach(landmark))
                    throw new InvalidOperationException("Cannot confirm the capture landmark approach.");
                for (var step = 0; step < 10000 && _travel.IsMoving; step++) _travel.Advance(0.1f);
                if (_travel.IsMoving || !_travel.CanInteractWith(landmark))
                    throw new InvalidOperationException("Cannot complete the capture landmark approach.");
                initial = _travel.Position;
            }
            if (!previewNorthTrail)
            {
                var world = _projection.FromArt(initial);
                _camera = new(new NumericsVector2(1280, MapViewportHeight), _projection.WorldSize,
                    new NumericsVector2(world.X - 640, world.Y - 300));
            }
        }
        _nightBlend = _campaign?.IsNight == true ? 1f : 0f;
        if (restoreWarning is not null) _interactionMessage = restoreWarning;
        if (startupMessage is not null) _interactionMessage = startupMessage;
        if ((restoreSave is null || _campaign?.IsVictory == true) && campaign is not null &&
            _saveProgress is not null && campaignId is not null)
            SaveProgress();
    }

    internal void CaptureRecruitmentPurchasePreview() => _recruitmentScreen?.CapturePurchasePreview();

    private float _nightBlend;
    private bool _previewNight;
    internal void PrepareNightPreview() { _previewNight = true; _nightBlend = 1f; }

    public void Update(MouseState mouse, KeyboardState keyboard, float elapsedSeconds)
    {
        _nightBlend = BattleLighting.AdvanceFade(_nightBlend,
            _previewNight || _campaign?.IsNight == true ? 1f : 0f, elapsedSeconds);
        _idleSeconds = (_idleSeconds + Math.Clamp(elapsedSeconds, 0f, 0.1f)) % 60f;
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

        var clicked = mouse.LeftButton == ButtonState.Pressed &&
                      _previousMouse.LeftButton == ButtonState.Released;
        if (IsPressed(keyboard, _previousKeyboard, Keys.F3)) ShowNavigation = !ShowNavigation;
        if (_openPanel != ExplorationPanelKind.None)
        {
            if (IsPressed(keyboard, _previousKeyboard, Keys.Escape) ||
                IsPressed(keyboard, _previousKeyboard, Keys.Back) ||
                clicked && ExplorationPanels.CloseButton.Contains(mouse.Position) ||
                _openPanel == ExplorationPanelKind.Map && IsPressed(keyboard, _previousKeyboard, Keys.M) ||
                _openPanel == ExplorationPanelKind.Journal && IsPressed(keyboard, _previousKeyboard, Keys.J))
                _openPanel = ExplorationPanelKind.None;
            else if (IsPressed(keyboard, _previousKeyboard, Keys.M)) _openPanel = ExplorationPanelKind.Map;
            else if (IsPressed(keyboard, _previousKeyboard, Keys.J)) _openPanel = ExplorationPanelKind.Journal;
            _previousMouse = mouse;
            _previousKeyboard = keyboard;
            return;
        }

        if (IsPressed(keyboard, _previousKeyboard, Keys.M) ||
            clicked && ExplorationHud.MapButton.Contains(mouse.Position))
            _openPanel = ExplorationPanelKind.Map;
        else if (IsPressed(keyboard, _previousKeyboard, Keys.J) ||
                 clicked && ExplorationHud.JournalButton.Contains(mouse.Position))
            _openPanel = ExplorationPanelKind.Journal;
        if (_openPanel != ExplorationPanelKind.None)
        {
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
        if (_travel is not null)
        {
            UpdateFreeTravel(pointer, clicked, keyboard, elapsedSeconds);
            _previousMouse = mouse;
            _previousKeyboard = keyboard;
            return;
        }
        _camera.CenterOn(_projection.Project(_movement.Position, _map.GetTile(_movement.Position)!.Height));
        if (mouse.Y < MapViewportHeight && mouse.X >= 0 && mouse.X < 1280)
        {
            _hover = _projection.TryPick(_camera.ToWorld(pointer), out var coordinate) ? coordinate : null;
        }
        else _hover = null;

        if (IsPressed(keyboard, _previousKeyboard, Keys.E)) InspectCurrentPointOfInterest();
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
        if (_travel is not null)
        {
            _travel.Stop();
            _travelPoint = null;
            _movement = new MovementSelection(CreateMovementMap(point.Coordinate), point.Coordinate);
        }
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
                () => _recruitmentScreen = null, SaveProgress, _currentMouse, _currentKeyboard);
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
        if (_travel is not null && _map.Navigation is { } navigation)
        {
            _surface = new(navigation, _campaign.IsUnlocked);
            _travel = new(_surface, _travel.Position);
            _travelPoint = null;
        }
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
        if (_campaign is not null) _saveProgress?.Invoke(_campaign, _movement.Position, _travel?.Position);
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

        using (_canvas.PushTint(BattleLighting.ExplorationTint(_nightBlend)))
        _renderer.Draw(_map, _projection, _camera, _movement.Position,
            _movement.Path.IsReachable ? _movement.Path.Coordinates : Array.Empty<HexCoord>(), _hover,
            VisibleIdleActors(), _idleSeconds,
            drawHero: !_previewNorthTrail, travel: _travel, hoverPosition: _hoverPosition, showNavigation: ShowNavigation,
            activeQuestPoints: ActiveQuestPoints());
        DrawMapPlaque();
        var hoveredPoint = _travel is not null ? _hoverPoint : _hover is { } coordinate && _pointsByCoordinate.TryGetValue(coordinate, out var points)
            ? points.FirstOrDefault() : null;
        _hud.Draw(_hero, _hero.Army.Stacks, hoveredPoint, CurrentActionText(hoveredPoint));
        if (_campaign?.IsDefeated == true || _campaign?.IsVictory == true) DrawCampaignEndingPanel();
        _encounterScreen?.Draw();
        _questScreen?.Draw();
        if (_openPanel == ExplorationPanelKind.Map)
            _panels.DrawMap(_map, _projection, _campaign, _movement.Position, _travel?.Position, ShowNavigation, _nightBlend);
        else if (_openPanel == ExplorationPanelKind.Journal)
            _panels.DrawJournal(_campaign);
    }

    private IReadOnlyDictionary<string, string> VisibleIdleActors()
    {
        var actors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (_map.Id != "flora-01") return actors;
        actors["guide-dialogue"] = "unit.flora.thorn-guard.idle";
        if (_campaign is null) return actors;
        if (!_campaign.CompletedQuestIds.Contains("pasture-choice"))
            actors["pasture-request"] = "unit.human.militia.idle";
        if (_campaign.CanAccessRecruitmentSite("peasant-recruitment"))
            actors["peasant-recruitment"] = "unit.human.militia.idle";

        foreach (var encounter in _campaign.Encounters.Where(encounter => _campaign.CanAccessEncounter(encounter.Id)))
        {
            var unitIds = encounter.EnemyArmy.Select(troop => troop.UnitTypeId).ToHashSet(StringComparer.Ordinal);
            actors[encounter.PointOfInterestId] = unitIds.Contains("human.inquisitor")
                ? "unit.human.inquisitor.idle"
                : unitIds.Contains("human.banner-knight") ? "unit.human.banner-knight.idle"
                : unitIds.Contains("human.militia") ? "unit.human.militia.idle"
                : "unit.flora.thorn-guard.idle";
        }
        return actors;
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
        _font.Draw($"{(_previewNight || _campaign?.IsNight == true ? "НОЧЬ" : "ДЕНЬ")}   ЗАВЕРШЕНО БОЁВ: {_campaign?.CompletedBattleCount ?? 0}", 31, 49, new Color(187, 174, 137), 1);
    }

    private string CurrentActionText(WorldPointOfInterest? hoveredPoint)
    {
        if (_interactionMessage is not null) return _interactionMessage;
        if (_travel is not null)
        {
            if (_travel.IsMoving) return "В ПУТИ. ЛКМ — ВЫБРАТЬ НОВЫЙ МАРШРУТ.";
            if (_travel.Destination is not null)
                return _travel.Preview.IsReachable ? "ЛКМ ЕЩЁ РАЗ — ИДТИ ПО МАРШРУТУ" : "ПУТЬ ПЕРЕКРЫТ";
            if (NearestVisiblePoint(_travel.Position, AdventureTravel.InteractionRadius) is { } nearby)
                return hoveredPoint?.Id == nearby.Id
                    ? "ЛКМ / E — ОСМОТРЕТЬ МЕСТО" : "E — ОСМОТРЕТЬ БЛИЖАЙШЕЕ МЕСТО";
            return hoveredPoint is not null ? "ЛКМ — ПОДОЙТИ К МЕСТУ" : "ЛКМ — ВЫБРАТЬ ЛЮБУЮ ТОЧКУ ТРОПЫ";
        }
        if (hoveredPoint is not null && hoveredPoint.Coordinate != _movement.Position)
            return "ЛКМ — ПУТЬ  /  ЕЩЁ РАЗ — ИДТИ";
        if (_movement.Destination is not null)
            return _movement.Path.IsReachable ? $"МАРШРУТ: {_movement.Path.Cost} ШАГОВ" : "ПУТЬ ПЕРЕКРЫТ";
        return _pointsByCoordinate.ContainsKey(_movement.Position) ? "E — ОСМОТРЕТЬ МЕСТО" : "ЛКМ — ВЫБРАТЬ ЦЕЛЬ";
    }

    private void UpdateFreeTravel(NumericsVector2 pointer, bool clicked, KeyboardState keyboard, float elapsedSeconds)
    {
        var travel = _travel!;
        var arrived = travel.Advance(Math.Clamp(elapsedSeconds, 0, 0.1f));
        _camera.CenterOn(_projection.FromArt(travel.Position));
        if (arrived)
        {
            var target = _travelPoint;
            _travelPoint = null;
            if (target is not null) VisitPointAt(target.Coordinate);
            SaveProgress();
            if (HasOpenModal) return;
        }
        if (travel.IsMoving)
        {
            _travelSaveSeconds += elapsedSeconds;
            if (_travelSaveSeconds >= 2) { _travelSaveSeconds = 0; SaveProgress(); }
        }
        _hover = null;
        _hoverPosition = pointer.X >= 0 && pointer.X < 1280 && pointer.Y >= 0 && pointer.Y < MapViewportHeight
            ? _projection.ToArt(_camera.ToWorld(pointer)) : null;
        _hoverPoint = _hoverPosition is { } hover ? NearestVisiblePoint(hover, 16, hitSprites: true) : null;
        if (IsPressed(keyboard, _previousKeyboard, Keys.E) &&
            NearestVisiblePoint(travel.Position, AdventureTravel.InteractionRadius) is { } current)
        {
            VisitPointAt(current.Coordinate);
            SaveProgress();
        }
        if (!clicked || _hoverPosition is not { } position || HasOpenModal) return;
        _interactionMessage = null;
        var targetPosition = _hoverPoint is not null ? InteractionPosition(_hoverPoint) : position;
        if (_hoverPoint is not null && travel.CanInteractWith(targetPosition))
        {
            VisitPointAt(_hoverPoint.Coordinate);
            SaveProgress();
            return;
        }
        SaveProgress();
        _travelPoint = _hoverPoint;
        if (_hoverPoint is not null) travel.SelectApproach(targetPosition);
        else travel.Select(targetPosition);
    }

    private WorldPointOfInterest? NearestVisiblePoint(MapPosition position, float radius, bool hitSprites = false)
    {
        var actors = VisibleIdleActors();
        var activeQuests = ActiveQuestPoints();
        return _map.PointsOfInterest.Where(point => AdventureMapRenderer.IsPointVisible(point,
                _map.Id == "flora-01", actors, activeQuests) &&
                (point.Type != "recruitment" || _campaign?.CanAccessRecruitmentSite(point.RecruitmentSiteId ?? "") == true) &&
                (point.Type != "encounter" || _campaign?.Encounters.Any(encounter =>
                    encounter.PointOfInterestId == point.Id && _campaign.CanAccessEncounter(encounter.Id)) == true))
            .Select(point =>
            {
                var anchor = InteractionPosition(point);
                var actorHit = hitSprites && actors.ContainsKey(point.Id) && AdventureMapRenderer.IsIdleActorHit(
                    _projection.FromArt(position), _projection.FromArt(anchor), _map.Id == "flora-01",
                    activeQuests?.Contains(point.Id) == true);
                var buildingHit = hitSprites && point.Type == "recruitment" &&
                    RecruitmentPointArt.GetAssetKey(point.RecruitmentSiteId) is not null &&
                    RecruitmentPointArt.Bounds(new Point((int)_projection.FromArt(anchor).X,
                        (int)_projection.FromArt(anchor).Y), _map.Id == "flora-01")
                    .Contains((int)_projection.FromArt(position).X, (int)_projection.FromArt(position).Y);
                if (hitSprites) anchor = new(anchor.X, anchor.Y - 15);
                var distance = MapPosition.Distance(position, anchor);
                return (Point: point, Anchor: anchor, Distance: distance, Hit: actorHit || buildingHit || distance <= radius);
            })
            .Where(item => item.Hit && (hitSprites || _travel!.CanInteractWith(item.Anchor, radius)))
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Point.Type == "quest" ? 0 : 1)
            .Select(item => item.Point).FirstOrDefault();
    }

    private IReadOnlySet<string>? ActiveQuestPoints()
    {
        if (_campaign is null) return null;
        var completed = _campaign.CompletedQuestIds;
        return _campaign.Quests.Where(quest => !completed.Contains(quest.Id))
            .Select(quest => quest.PointOfInterestId).OfType<string>().ToHashSet(StringComparer.Ordinal);
    }

    private MapPosition InteractionPosition(WorldPointOfInterest point) => _projection.ToArt(
        _projection.ProjectPoint(point) + AdventureMapRenderer.IdleActorOffset(point.Id));

    internal void PrepareTravelPreview()
    {
        if (_map.Navigation is not { } navigation) return;
        _surface = new(navigation);
        _travel = new(_surface, new(1290, 545));
        _travel.Select(new(1485, 545));
        var world = _projection.FromArt(_travel.Position);
        _camera.CenterOn(world);
        _questScreen = null;
    }
}
