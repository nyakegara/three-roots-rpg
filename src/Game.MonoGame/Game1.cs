using Game.Core.Content;
using Game.MonoGame.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame;

internal sealed class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly string? _screenshotPath;
    private readonly bool _battlePreview;
    private readonly bool _previewDescent;
    private readonly bool _previewQuestHover;
    private readonly string? _previewRecruitmentSiteId;
    private readonly int _battleSeed;
    private readonly bool _night;
    private SpriteBatch _spriteBatch = null!;
    private PixelCanvas _canvas = null!;
    private AssetCatalog _assets = null!;
    private IGameScreen _screen = null!;
    private RenderTarget2D _frame = null!;

    public Game1(string? screenshotPath = null, bool battlePreview = false,
        bool previewDescent = false, bool previewQuestHover = false, string? previewRecruitmentSiteId = null,
        int battleSeed = 1729, bool night = false)
    {
        _screenshotPath = screenshotPath;
        _battlePreview = battlePreview;
        _previewDescent = previewDescent;
        _previewQuestHover = previewQuestHover;
        _previewRecruitmentSiteId = previewRecruitmentSiteId;
        _battleSeed = battleSeed;
        _night = night;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = false;
        Window.Title = "Три корня";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _canvas = new PixelCanvas(GraphicsDevice, _spriteBatch);
        _assets = new AssetCatalog(GraphicsDevice);
        if (_battlePreview)
        {
            _screen = new BattlePreviewScreen(_canvas, _assets, Exit, true, _previewDescent, _battleSeed, _night);
        }
        else
        {
            var (campaign, units) = LoadFloraCampaign();
            _screen = new ExplorationScreen(_canvas, _assets, campaign, units,
                _previewQuestHover, _previewRecruitmentSiteId);
        }
        _frame = new RenderTarget2D(GraphicsDevice, 1280, 720, false,
            SurfaceFormat.Color, DepthFormat.None);
    }

    private static (CampaignDefinition Campaign, UnitCatalog Units) LoadFloraCampaign()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Content");
        var unitsPath = Path.Combine(root, "units", "units.json");
        var unitsResult = UnitCatalog.Load(unitsPath);
        if (!unitsResult.Success || unitsResult.Catalog is null)
            throw new InvalidOperationException("Cannot load troop catalog: " + string.Join("; ", unitsResult.Errors));

        var campaignPath = Path.Combine(root, "campaigns", "flora-01.json");
        var campaignResult = new CampaignLoader(unitsResult.Catalog).Load(campaignPath);
        if (campaignResult.Success && campaignResult.Definition is not null)
            return (campaignResult.Definition, unitsResult.Catalog);

        throw new InvalidOperationException(
            "Cannot load the flora campaign: " + string.Join("; ", campaignResult.Errors));
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape) && _screen is not ExplorationScreen { HasOpenRecruitmentScreen: true })
            Exit();

        if (_screenshotPath is null)
            _screen.Update(Mouse.GetState(), keyboard, (float)gameTime.ElapsedGameTime.TotalSeconds);

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

        if (_screenshotPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_screenshotPath)!);
            using var stream = File.Create(_screenshotPath);
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
