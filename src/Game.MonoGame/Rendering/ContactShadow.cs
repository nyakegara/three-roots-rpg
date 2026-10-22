using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Small translucent contact at the visible sprite's ground edge.</summary>
internal static class ContactShadow
{
    public static void Draw(PixelCanvas canvas, Rectangle visible)
    {
        var radius = Math.Clamp((int)(visible.Width * 0.28f), 8, 34);
        var height = Math.Clamp(radius / 4, 2, 6);
        var x = visible.Center.X;
        var y = visible.Bottom - 1;
        FantasyArt.Oval(canvas, x, y, radius, height, Color.Black * 0.09f);
        FantasyArt.Oval(canvas, x, y, Math.Max(3, radius * 3 / 4), Math.Max(1, height * 2 / 3), Color.Black * 0.10f);
        FantasyArt.Oval(canvas, x, y, Math.Max(2, radius / 2), 1, Color.Black * 0.12f);
    }
}
