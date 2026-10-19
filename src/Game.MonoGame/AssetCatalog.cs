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
    private readonly Dictionary<string, Texture2D> _textures = new();

    public AssetCatalog(GraphicsDevice device)
    {
        foreach (var key in new[] { FloraHero, HumanGuard, Rock, FloraPortrait,
                     AncientOak, ForestTree, Treasure })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Content", "art", key + ".png");
            if (!File.Exists(path))
                continue;

            try
            {
                using var stream = File.OpenRead(path);
                _textures.Add(key, LoadPremultiplied(device, stream));
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                // An unreadable override should not prevent opening the debug map.
                System.Diagnostics.Trace.TraceWarning("Cannot load art '{0}': {1}", path, exception.Message);
            }
        }
    }

    private static Texture2D LoadPremultiplied(GraphicsDevice device, Stream stream)
    {
        // FromStream preserves straight PNG alpha; the shared batch uses AlphaBlend.
        var texture = Texture2D.FromStream(device, stream);
        try
        {
            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
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

    public bool TryDraw(PixelCanvas canvas, string key, Rectangle destination)
    {
        if (!_textures.TryGetValue(key, out var texture))
            return false;

        canvas.Texture(texture, destination);
        return true;
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
            texture.Dispose();
        _textures.Clear();
    }
}
