using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.MonoGame.Rendering;

/// <summary>Local replacement for the tower at the west end of the southern dam.</summary>
internal static class HumanBorderArt
{
    private const float ReferenceWidth = 6688;
    private const float ReferenceHeight = 3764;
    private static readonly Rectangle DamEditRegion = new(2700, 2350, 1024, 1024);

    public static void ApplyDamEditMask(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        for (var y = 0; y < texture.Height; y++)
        for (var x = 0; x < texture.Width; x++)
        {
            var sourceX = x * 1024f / texture.Width;
            var sourceY = y * 1024f / texture.Height;
            var dx = (sourceX - 420f) / 165f;
            var dy = (sourceY - 390f) / 195f;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            var alpha = Math.Clamp((1f - distance) / 0.18f, 0f, 1f);
            var pixel = pixels[y * texture.Width + x];
            pixels[y * texture.Width + x] = new Color(
                (byte)(pixel.R * alpha), (byte)(pixel.G * alpha),
                (byte)(pixel.B * alpha), (byte)(pixel.A * alpha));
        }
        texture.SetData(pixels);
    }

    public static void DrawDamEdit(PixelCanvas canvas, AssetCatalog assets, Rectangle fullMapDestination)
    {
        var destination = new Rectangle(
            fullMapDestination.X + (int)MathF.Round(DamEditRegion.X / ReferenceWidth * fullMapDestination.Width),
            fullMapDestination.Y + (int)MathF.Round(DamEditRegion.Y / ReferenceHeight * fullMapDestination.Height),
            Math.Max(1, (int)MathF.Round(DamEditRegion.Width / ReferenceWidth * fullMapDestination.Width)),
            Math.Max(1, (int)MathF.Round(DamEditRegion.Height / ReferenceHeight * fullMapDestination.Height)));
        assets.TryDraw(canvas, AssetCatalog.HumanBorderDamEdit, destination);
    }
}
