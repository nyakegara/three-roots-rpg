using Game.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests;

public sealed class BattleAnimationFeedbackTests
{
    [Fact]
    public void HunterAttack_PreservesCrouchInsteadOfStretchingItToStandingHeight()
    {
        var idle = BattleUnitSpriteLayout.Bounds("human.order-hunter", new Point(200, 300));
        var action = BattleUnitSpriteLayout.ActionBounds("human.order-hunter", idle);
        Assert.Equal(idle.Bottom, action.Bottom);
        Assert.Equal(idle.Center.X, action.Center.X);
        Assert.Equal(58, action.Height);
        Assert.Equal(69, action.Width);
    }

    [Theory]
    [InlineData("human.inquisitor")]
    [InlineData("human.banner-knight")]
    [InlineData("human.longbow")]
    [InlineData("human.militia")]
    public void OtherHumanPoses_KeepTheirExistingCalibration(string type)
    {
        var idle = BattleUnitSpriteLayout.Bounds(type, new Point(200, 300));
        var action = BattleUnitSpriteLayout.ActionBounds(type, idle);
        Assert.Equal(new Rectangle(idle.X - 11, idle.Y, idle.Width + 22, idle.Height), action);
    }

    [Fact]
    public void ActionFrameLayout_FitsVisibleCucumberArtToIdleHeightAndKeepsFeetOnGround()
    {
        var pixels = new Color[64 * 80];
        for (var y = 46; y < 72; y++)
        for (var x = 19; x < 43; x++)
            pixels[y * 64 + x] = Color.White;

        var visible = ActionFrameLayout.FindVisibleBounds(pixels, 64, new Rectangle(0, 0, 64, 80));
        var destination = ActionFrameLayout.FitToReference(visible, visible, new Rectangle(100, 134, 56, 66));

        Assert.Equal(new Rectangle(19, 46, 24, 26), visible);
        Assert.Equal(66, destination.Height);
        Assert.Equal(200, destination.Bottom);
        Assert.Equal(56, destination.Width);
        Assert.Equal(100, destination.X);
    }

    [Theory]
    [InlineData(55, false)]
    [InlineData(56, false)]
    [InlineData(55, true)]
    [InlineData(56, true)]
    public void NeutralAction_MatchesActualIdleDestinationDespiteDifferentAtlasAspectRatio(int width, bool flip)
    {
        var idleBounds = new Rectangle(100, 136, width, 63);
        Assert.Equal(idleBounds, ActionFrameLayout.FitToReference(
            new Rectangle(19, 46, 24, 26), new Rectangle(19, 46, 24, 26), idleBounds, flip));
    }

    [Fact]
    public void ProjectileRelease_StartsOnSameFrameAsBowReleasePose()
    {
        Assert.Equal(1, AttackAnimationTiming.FrameAt(AttackAnimationTiming.Duration -
            AttackAnimationTiming.ReleaseSeconds + 0.001f));
        Assert.Equal(AttackAnimationTiming.ReleaseFrame, AttackAnimationTiming.FrameAt(
            AttackAnimationTiming.Duration - AttackAnimationTiming.ReleaseSeconds));
        Assert.Equal(7, AttackAnimationTiming.FrameAt(0));
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
    public void SharedReferenceScale_KeepsCucumberBodySizeWhenWeaponRises()
    {
        var reference = new Rectangle(19, 46, 24, 26);
        var idle = ActionFrameLayout.FitToReference(reference, reference, new Rectangle(100, 134, 56, 66));
        var raised = ActionFrameLayout.FitToReference(new Rectangle(147, 40, 24, 32), reference,
            new Rectangle(100, 134, 56, 66));
        Assert.Equal(idle.Width, raised.Width);
        Assert.Equal(81, raised.Height);
        Assert.Equal(idle.Bottom, raised.Bottom);
        Assert.Equal(idle.X, raised.X);
    }

    [Fact]
    public void Mirroring_ReflectsWeaponOffsetAroundSameBodyPivot()
    {
        var reference = new Rectangle(19, 46, 24, 26);
        var swing = new Rectangle(21, 127, 35, 25);
        var bounds = new Rectangle(100, 134, 56, 66);
        var normal = ActionFrameLayout.FitToReference(swing, reference, bounds);
        var mirrored = ActionFrameLayout.FitToReference(swing, reference, bounds, flip: true);
        Assert.Equal(normal.Size, mirrored.Size);
        Assert.InRange(normal.X + mirrored.Right, 255, 257);
        Assert.Equal(normal.Bottom, mirrored.Bottom);
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
