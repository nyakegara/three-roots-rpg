using Game.Core.Content;
using Game.Core.Persistence;
using Game.Core.Geometry;
using Game.MonoGame.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame;

internal sealed class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameLaunchOptions _options;
    private KeyboardState _previousKeyboard;
    private SpriteBatch _spriteBatch = null!;
    private PixelCanvas _canvas = null!;
    private AssetCatalog _assets = null!;
    private IGameScreen _screen = null!;
    private RenderTarget2D _frame = null!;
    private CampaignDefinition? _floraCampaign;
    private UnitCatalog? _floraUnits;
    private CampaignProgressWriter? _progressWriter;

    public Game1(GameLaunchOptions options)
    {
        _options = options;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = options.ScreenshotPath is null;
        Window.AllowUserResizing = false;
        Window.Title = "Три корня";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _canvas = new PixelCanvas(GraphicsDevice, _spriteBatch);
        _assets = new AssetCatalog(GraphicsDevice);
        if (_options.PlayableCombat)
        {
            var units = LoadUnitCatalog();
            _screen = new BattleScreen(_canvas, _assets, units, _options.BattleSeed, _options.Night, Exit, _options.CombatCapture);
        }
        else if (_options.BattlePreview)
        {
            _screen = new BattlePreviewScreen(_canvas, _assets, Exit, true, _options.PreviewDescent, _options.BattleSeed, _options.Night);
        }
        else
        {
            var (campaign, units) = LoadFloraCampaign();
            _floraCampaign = campaign;
            _floraUnits = units;
            CampaignSave? restoreSave = null;
            string? saveWarning = null;
            var canWriteSave = true;
            var savePath = CampaignSavePath();
            if (_options.ScreenshotPath is null && File.Exists(savePath))
            {
                var load = CampaignSerializer.Load(savePath);
                if (load.Success) restoreSave = load.Save;
                else
                {
                    System.Diagnostics.Trace.TraceWarning("Campaign save was not loaded: {0}", load.Message);
                    canWriteSave = PreserveInvalidSave(savePath);
                    saveWarning = canWriteSave
                        ? "СТАРОЕ СОХРАНЕНИЕ НЕ ПРОЧИТАНО. НАЧИНАЕТСЯ НОВАЯ КАМПАНИЯ."
                        : "СОХРАНЕНИЕ ПОВРЕЖДЕНО. ФАЙЛ СОХРАНЁН БЕЗ ИЗМЕНЕНИЙ, НОВЫЙ ПРОГРЕСС НЕ АВТОСОХРАНЯЕТСЯ.";
                }
            }
            if (restoreSave is not null && !campaign.CreateGameState().TryRestoreSave(restoreSave,
                    campaign.Id, campaign.PlayerFactionId, campaign.Map, units, out var restoreError))
            {
                System.Diagnostics.Trace.TraceWarning("Campaign save was rejected: {0}", restoreError);
                restoreSave = null;
                canWriteSave = PreserveInvalidSave(savePath);
                saveWarning = canWriteSave
                    ? "СОХРАНЕНИЕ НЕ СОВПАДАЕТ С ЭТОЙ ВЕРСИЕЙ КАМПАНИИ. НАЧИНАЕТСЯ НОВАЯ ИГРА."
                    : "СОХРАНЕНИЕ НЕ СОВПАДАЕТ С ВЕРСИЕЙ ИГРЫ И НЕ ИЗМЕНЕНО. НОВЫЙ ПРОГРЕСС НЕ АВТОСОХРАНЯЕТСЯ.";
            }
            if (_options.ScreenshotPath is null && canWriteSave)
                _progressWriter = NewProgressWriter(campaign, restoreSave?.RunId);
            _screen = new ExplorationScreen(_canvas, _assets, campaign, units,
                _options.PreviewQuestHover, _options.PreviewRecruitmentSiteId, Exit, _options.CampaignCapture,
                _options.PreviewNorthTrail, _options.PreviewIdleMap,
                _options.PreviewMapPanel, _options.PreviewJournalPanel, restoreSave,
                _progressWriter is not null ? SaveCampaignProgress : null,
                saveWarning) { ShowNavigation = _options.PreviewNavigation };
            if (_options.PreviewFreeTravel) ((ExplorationScreen)_screen).PrepareTravelPreview();
        }
        _frame = new RenderTarget2D(GraphicsDevice, 1280, 720, false,
            SurfaceFormat.Color, DepthFormat.None);
    }

    private static (CampaignDefinition Campaign, UnitCatalog Units) LoadFloraCampaign()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Content");
        var units = LoadUnitCatalog();
        var campaignPath = Path.Combine(root, "campaigns", "flora-01.json");
        var campaignResult = new CampaignLoader(units).Load(campaignPath);
        if (campaignResult.Success && campaignResult.Definition is not null)
            return (campaignResult.Definition, units);

        throw new InvalidOperationException(
            "Cannot load the flora campaign: " + string.Join("; ", campaignResult.Errors));
    }

    private static UnitCatalog LoadUnitCatalog()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Content");
        var unitsPath = Path.Combine(root, "units", "units.json");
        var unitsResult = UnitCatalog.Load(unitsPath);
        if (!unitsResult.Success || unitsResult.Catalog is null)
            throw new InvalidOperationException("Cannot load troop catalog: " + string.Join("; ", unitsResult.Errors));

        return unitsResult.Catalog;
    }

    private static string CampaignSavePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreeRoots", "saves", "flora-01.json");

    private static string LeaderboardPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreeRoots", "leaderboard.json");

    private static CampaignProgressWriter NewProgressWriter(CampaignDefinition campaign, string? runId = null) =>
        new(CampaignSavePath(), LeaderboardPath(), campaign.Id, campaign.Map.Id, runId);

    private static bool PreserveInvalidSave(string savePath)
    {
        try
        {
            var backupPath = savePath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".bak";
            File.Copy(savePath, backupPath, overwrite: false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning("Could not preserve the invalid save: {0}", exception.Message);
            return false;
        }
    }

    private void SaveCampaignProgress(Game.Core.Campaigns.Campaign campaign, HexCoord position,
        Game.Core.Navigation.MapPosition? adventurePosition)
    {
        var result = _progressWriter!.Save(campaign, position, adventurePosition);
        if (!result.SaveSucceeded || campaign.IsVictory && !result.VictoryRecordSucceeded)
            System.Diagnostics.Trace.TraceWarning("Campaign progress write: {0}", result.Message);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (_screen is ExplorationScreen exploration &&
            (exploration.IsCampaignDefeated || exploration.IsCampaignVictory) &&
            keyboard.IsKeyDown(Keys.R) && !_previousKeyboard.IsKeyDown(Keys.R) &&
            _floraCampaign is not null && _floraUnits is not null)
        {
            if (_progressWriter is not null) _progressWriter = NewProgressWriter(_floraCampaign);
            _screen = new ExplorationScreen(_canvas, _assets, _floraCampaign, _floraUnits,
                exit: Exit, saveProgress: _progressWriter is not null ? SaveCampaignProgress : null);
        }
        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape) &&
            _screen is not ExplorationScreen { HasOpenModal: true })
            Exit();

        if (_options.ScreenshotPath is null)
            _screen.Update(Mouse.GetState(), keyboard, (float)gameTime.ElapsedGameTime.TotalSeconds);
        _previousKeyboard = keyboard;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_frame);
        GraphicsDevice.Clear(ExplorationScreen.Background);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend,
            rasterizerState: ScissorRasterizer);
        _screen.Draw();
        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);

        GraphicsDevice.Clear(ExplorationScreen.Background);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp,
            blendState: BlendState.Opaque);
        _spriteBatch.Draw(_frame, Vector2.Zero, Color.White);
        _spriteBatch.End();
        base.Draw(gameTime);

        if (_options.ScreenshotPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_options.ScreenshotPath)!);
            using var stream = File.Create(_options.ScreenshotPath);
            _frame.SaveAsPng(stream, _frame.Width, _frame.Height);
            Exit();
        }
    }

    private static readonly RasterizerState ScissorRasterizer = new() { ScissorTestEnable = true };

    protected override void UnloadContent()
    {
        _frame.Dispose();
        _assets.Dispose();
        _canvas.Dispose();
        _spriteBatch.Dispose();
        base.UnloadContent();
    }
}
