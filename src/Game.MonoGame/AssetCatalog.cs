using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
    public const string FloraIslandNorthWest = "exploration.flora-island-v3-nw";
    public const string FloraIslandNorthEast = "exploration.flora-island-v3-ne";
    public const string FloraIslandSouthWest = "exploration.flora-island-v3-sw";
    public const string FloraIslandSouthEast = "exploration.flora-island-v3-se";
    public const string RootGreenhouse = "poi.flora.root-greenhouse";
    public const string SunnyClearing = "poi.flora.sunny-clearing";
    public const string SporeCircle = "poi.flora.spore-circle";
    public const string BattlefieldForestDay = "battlefield.forest.day";
    public const string BattlefieldForestNight = "battlefield.forest.night";
    private readonly Dictionary<string, TextureAsset> _textures = new();

    public AssetCatalog(GraphicsDevice device)
    {
        foreach (var key in new[] { FloraHero, HumanGuard, Rock, FloraPortrait,
                     AncientOak, ForestTree, Treasure, AdventureGround, FloraIslandGround,
                     FloraIslandNorthWest, FloraIslandNorthEast,
                     FloraIslandSouthWest, FloraIslandSouthEast,
                     "unit.flora.sprout", "unit.flora.thorn-guard", "unit.flora.ember-bloom",
                     "unit.flora.pod-slinger", "unit.flora.root-warden",
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

    public bool TryDraw(PixelCanvas canvas, string key, Rectangle destination, float opacity = 1f)
    {
        if (!_textures.TryGetValue(key, out var asset))
            return false;

        canvas.Texture(asset.Texture, destination, opacity);
        return true;
    }

    public bool TryDrawCropped(PixelCanvas canvas, string key, Rectangle bounds)
    {
        if (!_textures.TryGetValue(key, out var asset))
            return false;

        var source = asset.ContentBounds;
        var scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
        var width = Math.Max(1, (int)MathF.Round(source.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(source.Height * scale));
        var destination = new Rectangle(bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height);
        canvas.Texture(asset.Texture, source, destination);
        return true;
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
        foreach (var asset in _textures.Values)
            asset.Texture.Dispose();
        _textures.Clear();
    }

    private sealed record TextureAsset(Texture2D Texture, Rectangle ContentBounds);
}
