using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Game.MonoGame.Rendering;

namespace Game.MonoGame;

/// <summary>
/// Optional, cached art overrides. Keys are filenames under the executable's Content/art
/// directory; absent files retain the procedural preview art. No working-directory dependency.
/// </summary>
internal sealed class AssetCatalog : IDisposable
{
    public const string FloraHero = "hero.flora";
    public const string HumanGuard = "unit.human.guard";
    public const string Rock = "terrain.rock";
    public const string FloraPortrait = "portrait.flora";
    public const string AncientOak = "terrain.old-oak";
    public const string ForestTree = "terrain.forest-tree";
    public const string Treasure = "poi.treasure";
    public const string AdventureGround = "exploration.ground";
    public const string FloraIslandGround = "exploration.flora-island";
    public const string FloraIslandNorthWest = "exploration.flora-island-v5-nw";
    public const string FloraIslandNorthEast = "exploration.flora-island-v5-ne";
    public const string FloraIslandSouthWest = "exploration.flora-island-v5-sw";
    public const string FloraIslandSouthEast = "exploration.flora-island-v5-se";
    public const string RootGreenhouse = "poi.flora.root-greenhouse";
    public const string SunnyClearing = "poi.flora.sunny-clearing";
    public const string SporeCircle = "poi.flora.spore-circle";
    public const string BattlefieldForestDay = "battlefield.forest.day";
    public const string BattlefieldForestNight = "battlefield.forest.night";
    private readonly Dictionary<string, TextureAsset> _textures = new();
    private readonly Dictionary<string, Texture2D> _idleTextures = new();
    private readonly Dictionary<string, Texture2D> _actionTextures = new();
    private readonly Dictionary<string, Rectangle[]> _actionFrameBounds = new();
    private Texture2D? _heroWalkTexture;

    public AssetCatalog(GraphicsDevice device)
    {
        foreach (var key in new[] { FloraHero, HumanGuard, Rock, FloraPortrait,
                     AncientOak, ForestTree, Treasure, AdventureGround, FloraIslandGround,
                     FloraIslandNorthWest, FloraIslandNorthEast,
                     FloraIslandSouthWest, FloraIslandSouthEast,
                     "unit.flora.sprout", "unit.flora.thorn-guard", "unit.flora.ember-bloom",
                     "unit.flora.pod-slinger", "unit.flora.root-warden",
                     "unit.human.militia", "unit.human.longbow", "unit.human.banner-knight",
                     "unit.human.order-hunter", "unit.human.inquisitor",
                     "unit.human.militia.attack", "unit.human.longbow.attack",
                     "unit.human.banner-knight.attack", "unit.human.order-hunter.attack",
                     "unit.human.inquisitor.attack", "unit.human.inquisitor.cast", "unit.human.longbow.released",
                     RootGreenhouse, SunnyClearing, SporeCircle,
                     BattlefieldForestDay, BattlefieldForestNight })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Content", "art", key + ".png");
            if (!File.Exists(path))
                continue;

            try
            {
                using var stream = File.OpenRead(path);
                var texture = LoadPremultiplied(device, stream, out var contentBounds);
                _textures.Add(key, new TextureAsset(texture, contentBounds));
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                // An unreadable override should not prevent opening the debug map.
                System.Diagnostics.Trace.TraceWarning("Cannot load art '{0}': {1}", path, exception.Message);
            }
        }

        foreach (var key in new[] { "unit.flora.thorn-guard.idle", "unit.human.militia.idle",
                     "unit.human.banner-knight.idle", "unit.human.inquisitor.idle" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Content", "art", "animations", key + ".png");
            if (!File.Exists(path)) continue;
            try
            {
                using var stream = File.OpenRead(path);
                var texture = LoadPremultiplied(device, stream, out _);
                if (texture.Width == 256 && texture.Height == 80) _idleTextures.Add(key, texture);
                else texture.Dispose();
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                System.Diagnostics.Trace.TraceWarning("Cannot load idle art '{0}': {1}", path, exception.Message);
            }
        }
        foreach (var key in new[] { "unit.flora.sprout.actions", "unit.flora.thorn-guard.actions",
                     "unit.flora.ember-bloom.actions", "unit.flora.pod-slinger.actions",
                     "unit.flora.root-warden.actions", "unit.human.militia.actions", "unit.human.longbow.actions",
                     "unit.human.banner-knight.actions", "unit.human.order-hunter.actions",
                     "unit.human.inquisitor.actions" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Content", "art", "animations", key + ".png");
            if (!File.Exists(path)) continue;
            try
            {
                using var stream = File.OpenRead(path);
                var texture = LoadPremultiplied(device, stream, out _);
                if (texture.Width == 256 && texture.Height is 160 or 320)
                {
                    var frames = new Rectangle[texture.Height / 80 * 4];
                    var pixels = new Color[texture.Width * texture.Height];
                    texture.GetData(pixels);
                    for (var index = 0; index < frames.Length; index++)
                    {
                        var frame = new Rectangle(index % 4 * 64, index / 4 * 80, 64, 80);
                        frames[index] = ActionFrameLayout.FindVisibleBounds(pixels, texture.Width, frame);
                    }
                    _actionTextures.Add(key, texture);
                    _actionFrameBounds.Add(key, frames);
                }
                else texture.Dispose();
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                System.Diagnostics.Trace.TraceWarning("Cannot load action art '{0}': {1}", path, exception.Message);
            }
        }
        var heroPath = Path.Combine(AppContext.BaseDirectory, "Content", "art", "animations", "hero.flora.walk.png");
        if (File.Exists(heroPath))
        {
            try
            {
                using var stream = File.OpenRead(heroPath);
                var texture = LoadPremultiplied(device, stream, out _);
                if (texture.Width == 256 && texture.Height == 720) _heroWalkTexture = texture;
                else texture.Dispose();
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                System.Diagnostics.Trace.TraceWarning("Cannot load hero animation: {0}", exception.Message);
            }
        }
    }

    private static Texture2D LoadPremultiplied(GraphicsDevice device, Stream stream, out Rectangle contentBounds)
    {
        // FromStream preserves straight PNG alpha; the shared batch uses AlphaBlend.
        var texture = Texture2D.FromStream(device, stream);
        try
        {
            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            contentBounds = FindVisibleBounds(pixels, texture.Width, texture.Height);
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
            texture.SetData(pixels);
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    public bool HasAsset(string key) => _textures.ContainsKey(key);

    public bool TryDrawHeroWalk(PixelCanvas canvas, Rectangle destination,
        bool moving, System.Numerics.Vector2 direction, float seconds)
    {
        if (_heroWalkTexture is null) return false;
        var octant = ((int)MathF.Round(MathF.Atan2(direction.Y, direction.X) / (MathF.PI / 4)) + 8) % 8;
        var row = moving ? (octant + 2) % 8 : 8;
        var cardinal = row is 0 or 4;
        var frame = moving ? (int)(seconds / (cardinal ? 0.44f : 0.22f)) % (cardinal ? 2 : 4) : 0;
        canvas.Texture(_heroWalkTexture, new Rectangle(frame * 64, row * 80, 64, 80), destination);
        return true;
    }

    public bool TryDrawIdle(PixelCanvas canvas, string key, Rectangle destination, int frame)
    {
        if (!_idleTextures.TryGetValue(key, out var texture)) return false;
        canvas.Texture(texture, new Rectangle(Math.Abs(frame % 4) * 64, 0, 64, 80), destination);
        return true;
    }

    public bool TryDrawAction(PixelCanvas canvas, string key, Rectangle destination, int frame,
        bool secondary, bool flipHorizontally)
    {
        if (!_actionTextures.TryGetValue(key, out var texture)) return false;
        var index = Math.Clamp(frame, 0, 7) + (secondary && texture.Height == 320 ? 8 : 0);
        var source = _actionFrameBounds[key][index];
        if (source.IsEmpty)
            source = new Rectangle(index % 4 * 64, index / 4 * 80, 64, 80);
        var reference = _actionFrameBounds[key][0];
        if (reference.IsEmpty) reference = new Rectangle(0, 0, 64, 80);
        var fitted = ActionFrameLayout.FitToReference(source, reference, destination, flipHorizontally);
        canvas.Texture(texture, source, fitted, flipHorizontally);
        return true;
    }

    public bool TryDraw(PixelCanvas canvas, string key, Rectangle destination, float opacity = 1f)
    {
        if (!_textures.TryGetValue(key, out var asset))
            return false;

        canvas.Texture(asset.Texture, destination, opacity);
        return true;
    }

    public bool TryDrawCropped(PixelCanvas canvas, string key, Rectangle bounds, bool flipHorizontally = false)
    {
        if (!_textures.TryGetValue(key, out var asset))
            return false;

        canvas.Texture(asset.Texture, asset.ContentBounds, GetCroppedDestination(key, bounds), flipHorizontally);
        return true;
    }

    public Rectangle GetCroppedDestination(string key, Rectangle bounds)
    {
        if (!_textures.TryGetValue(key, out var asset)) return bounds;
        var source = asset.ContentBounds;
        var scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
        var width = Math.Max(1, (int)MathF.Round(source.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(source.Height * scale));
        return new Rectangle(bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height);
    }

    private static Rectangle FindVisibleBounds(IReadOnlyList<Color> pixels, int width, int height)
    {
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * width + x].A < 128) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return new Rectangle(0, 0, width, height);

        const int edgePadding = 2;
        minX = Math.Max(0, minX - edgePadding);
        minY = Math.Max(0, minY - edgePadding);
        maxX = Math.Min(width - 1, maxX + edgePadding);
        maxY = Math.Min(height - 1, maxY + edgePadding);
        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public void Dispose()
    {
        _heroWalkTexture?.Dispose();
        foreach (var asset in _textures.Values)
            asset.Texture.Dispose();
        _textures.Clear();
        foreach (var texture in _idleTextures.Values) texture.Dispose();
        _idleTextures.Clear();
        foreach (var texture in _actionTextures.Values) texture.Dispose();
        _actionTextures.Clear();
        _actionFrameBounds.Clear();
    }

    private sealed record TextureAsset(Texture2D Texture, Rectangle ContentBounds);
}
