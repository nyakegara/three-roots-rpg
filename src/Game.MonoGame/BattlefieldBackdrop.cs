using Microsoft.Xna.Framework;

namespace Game.MonoGame;

/// <summary>Day/night background artwork for tactical battlefields.</summary>
internal static class BattlefieldBackdrop
{
    private static readonly Rectangle Bounds = new(0, 0, 1280, 610);

    public static void Draw(PixelCanvas canvas, AssetCatalog assets, float nightBlend)
    {
        nightBlend = float.IsFinite(nightBlend) ? Math.Clamp(nightBlend, 0f, 1f) : 0f;
        var hasDay = assets.HasAsset(AssetCatalog.BattlefieldForestDay);
        var hasNight = assets.HasAsset(AssetCatalog.BattlefieldForestNight);
        if (hasDay && hasNight)
        {
            assets.TryDraw(canvas, AssetCatalog.BattlefieldForestDay, Bounds);
            assets.TryDraw(canvas, AssetCatalog.BattlefieldForestNight, Bounds, nightBlend);
            return;
        }

        // If only one override exists, show it at full opacity instead of blending through a hole.
        var available = hasNight ? AssetCatalog.BattlefieldForestNight : AssetCatalog.BattlefieldForestDay;
        if (hasDay || hasNight)
        {
            assets.TryDraw(canvas, available, Bounds);
            return;
        }

        FantasyArt.DrawLandscape(canvas);
    }
}
