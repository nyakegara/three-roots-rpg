using Game.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests;

public sealed class BattleAnimationFeedbackTests
{
    [Fact]
    public void ActionFrameLayout_FitsVisibleCucumberArtToIdleHeightAndKeepsFeetOnGround()
    {
        var pixels = new Color[64 * 80];
        for (var y = 46; y < 72; y++)
        for (var x = 19; x < 43; x++)
            pixels[y * 64 + x] = Color.White;

        var visible = ActionFrameLayout.FindVisibleBounds(pixels, 64, new Rectangle(0, 0, 64, 80));
        var destination = ActionFrameLayout.FitToHeight(visible, new Rectangle(100, 134, 56, 66));

        Assert.Equal(new Rectangle(19, 46, 24, 26), visible);
        Assert.Equal(66, destination.Height);
        Assert.Equal(200, destination.Bottom);
        Assert.Equal(61, destination.Width);
        Assert.Equal(98, destination.X);
    }

    [Fact]
    public void ActionFrameLayout_IgnoresAlphaBelowCutoffAndStaysInsideRequestedFrame()
    {
        var pixels = new Color[128 * 80];
        for (var y = 12; y < 22; y++)
        for (var x = 73; x < 91; x++)
            pixels[y * 128 + x] = new Color(255, 255, 255, 127);
        for (var y = 30; y < 44; y++)
        for (var x = 82; x < 101; x++)
            pixels[y * 128 + x] = Color.White;

        var visible = ActionFrameLayout.FindVisibleBounds(pixels, 128, new Rectangle(64, 0, 64, 80));

        Assert.Equal(new Rectangle(82, 30, 19, 14), visible);
    }

    [Fact]
    public void MeleeSlashCenter_StaysBesideAttackerInsteadOfTravelingToTarget()
    {
        var start = new Vector2(100, 200);
        var nearTarget = BattleAttackEffect.GetMeleeSlashCenter(start, new Vector2(130, 200));
        var farTarget = BattleAttackEffect.GetMeleeSlashCenter(start, new Vector2(400, 200));

        Assert.Equal(nearTarget, farTarget);
        Assert.InRange(Vector2.Distance(start, farTarget), 0, 30);
    }
}
