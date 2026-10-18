using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame;

internal sealed class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly string? _screenshotPath;
    private readonly bool _showGrid;
    private readonly bool _previewDescent;
    private SpriteBatch _spriteBatch = null!;
    private PixelCanvas _canvas = null!;
    private AssetCatalog _assets = null!;
    private BattlePreviewScreen _screen = null!;
    private RenderTarget2D _frame = null!;

    public Game1(string? screenshotPath = null, bool showGrid = true, bool previewDescent = false)
    {
        _screenshotPath = screenshotPath;
        _showGrid = showGrid;
        _previewDescent = previewDescent;
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
        _screen = new BattlePreviewScreen(_canvas, _assets, Exit, _showGrid, _previewDescent);
        _frame = new RenderTarget2D(GraphicsDevice, 1280, 720, false,
            SurfaceFormat.Color, DepthFormat.None);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // Capture the same renderer with its deterministic initial selection.
        if (_screenshotPath is null)
            _screen.Update(Mouse.GetState(), Keyboard.GetState());

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_frame);
        GraphicsDevice.Clear(BattlePreviewScreen.Background);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);
        _screen.Draw();
        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);

        GraphicsDevice.Clear(BattlePreviewScreen.Background);
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

    protected override void UnloadContent()
    {
        _frame.Dispose();
        _assets.Dispose();
        _canvas.Dispose();
        _spriteBatch.Dispose();
        base.UnloadContent();
    }
}
