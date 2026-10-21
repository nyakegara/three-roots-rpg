using Game.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests;

public sealed class SpriteHitTestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReducedImage_SamplesPixelCentreRatherThanLeftEdge(bool flip)
    {
        byte[] alpha = [0, 255];
        Assert.True(SpriteHitTest.IsVisible(alpha, 2, new Rectangle(0, 0, 2, 1),
            new Rectangle(0, 0, 1, 1), new Point(0, 0), flip));
    }

    [Fact]
    public void BattleSpriteSizes_KeepCommonGroundAndApprovedRelativeScale()
    {
        var center = new Point(400, 300);
        var cucumber = BattleUnitSpriteLayout.Bounds("flora.thorn-guard", center);
        var knight = BattleUnitSpriteLayout.Bounds("human.banner-knight", center);
        var inquisitor = BattleUnitSpriteLayout.Bounds("human.inquisitor", center);
        var mushroom = BattleUnitSpriteLayout.Bounds("flora.root-warden", center);
        Assert.Equal(cucumber.Bottom, knight.Bottom);
        Assert.Equal(cucumber.Bottom, inquisitor.Bottom);
        Assert.Equal(cucumber.Bottom, mushroom.Bottom);
        Assert.True(cucumber.Height < knight.Height && knight.Height < inquisitor.Height);
        Assert.True(mushroom.Height > cucumber.Height);
        var label = BattleUnitSpriteLayout.CountLabel("human.banner-knight", 10000);
        Assert.True(BattleUnitSpriteLayout.CountBounds(label, center).Contains(new Point(453, 318)));
    }

    [Fact]
    public void Hit_UsesCroppedArtAndLeavesTransparentPixelsClickableThrough()
    {
        byte[] alpha = [0, 0, 0, 0, 0, 0,
                        0, 0, 255, 0, 0, 0,
                        0, 0, 255, 255, 0, 0,
                        0, 0, 0, 0, 0, 0];
        var source = new Rectangle(2, 1, 2, 2);
        var drawn = new Rectangle(100, 200, 20, 20);
        Assert.True(SpriteHitTest.IsVisible(alpha, 6, source, drawn, new Point(105, 205)));
        Assert.False(SpriteHitTest.IsVisible(alpha, 6, source, drawn, new Point(115, 205)));
        Assert.True(SpriteHitTest.IsVisible(alpha, 6, source, drawn, new Point(115, 215)));
        Assert.False(SpriteHitTest.IsVisible(alpha, 6, source, drawn, new Point(120, 215)));
        Assert.False(SpriteHitTest.IsVisible(alpha, 6, source, drawn, new Point(99, 205)));
    }

    [Fact]
    public void EnemyMirroring_UsesSameVisiblePixelsAsFlippedDrawing()
    {
        byte[] alpha = [255, 0];
        var source = new Rectangle(0, 0, 2, 1);
        var drawn = new Rectangle(10, 10, 20, 10);
        Assert.False(SpriteHitTest.IsVisible(alpha, 2, source, drawn, new Point(15, 15), flip: true));
        Assert.True(SpriteHitTest.IsVisible(alpha, 2, source, drawn, new Point(25, 15), flip: true));
    }

    [Fact]
    public void FaintAssetFringe_DoesNotStealClicksFromTerrain()
    {
        byte[] alpha = [127, 128];
        var box = new Rectangle(0, 0, 2, 1);
        Assert.False(SpriteHitTest.IsVisible(alpha, 2, box, box, new Point(0, 0)));
        Assert.True(SpriteHitTest.IsVisible(alpha, 2, box, box, new Point(1, 0)));
    }
}
