using Game.MonoGame;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests;

public sealed class BattleLightingTests
{
    [Fact]
    public void AdvanceFadeMovesAtTimedRateWithoutOvershooting()
    {
        Assert.Equal(0.25f, BattleLighting.AdvanceFade(0f, 1f, 0.3f), 3);
        Assert.Equal(0.75f, BattleLighting.AdvanceFade(1f, 0f, 0.3f), 3);
        Assert.Equal(1f, BattleLighting.AdvanceFade(0.9f, 1f, 10f));
        Assert.Equal(0f, BattleLighting.AdvanceFade(0.1f, 0f, 10f));
    }

    [Fact]
    public void AmbientPaletteHasDayAndReadableCoolNightEndpoints()
    {
        Assert.Equal(BattleLighting.DayTint, BattleLighting.AmbientTint(0f));
        var night = BattleLighting.AmbientTint(1f);

        Assert.Equal(BattleLighting.NightTint, night);
        Assert.True(night.B > night.G);
        Assert.True(night.R < BattleLighting.DayTint.R);
    }

    [Fact]
    public void ForegroundRemainsBrighterThanMoonlitGround()
    {
        var ground = BattleLighting.AmbientTint(1f);
        var foreground = BattleLighting.ForegroundTint(1f);

        Assert.Equal(BattleLighting.DayTint, BattleLighting.ForegroundTint(0f));
        Assert.True(foreground.R > ground.R);
        Assert.True(foreground.G > ground.G);
        Assert.True(foreground.B > ground.B);
        Assert.True(foreground.B > foreground.G);
    }

    [Fact]
    public void TintChangesRgbWhileKeepingSourceAlpha()
    {
        var source = new Color(200, 150, 100, 37);
        var tint = new Color(128, 255, 64, 12);

        Assert.Equal(new Color(100, 150, 25, 37),
            BattleLighting.TintPreservingAlpha(source, tint));
    }

    [Fact]
    public void AdvanceFadeClampsInputsAndHandlesInvalidDuration()
    {
        Assert.Equal(0f, BattleLighting.AdvanceFade(-2f, 2f, 0f));
        Assert.Equal(1f, BattleLighting.AdvanceFade(0.2f, 1f, 0.1f, 0f));
        Assert.Equal(0.2f, BattleLighting.AdvanceFade(0.2f, 0f, float.NaN));
    }
}
