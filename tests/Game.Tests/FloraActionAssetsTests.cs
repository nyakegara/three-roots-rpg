using Xunit;

namespace Game.Tests;

public sealed class FloraActionAssetsTests
{
    [Theory]
    [InlineData("unit.human.militia.idle.v2.png")]
    [InlineData("unit.human.banner-knight.idle.v2.png")]
    [InlineData("unit.human.inquisitor.idle.v2.png")]
    public void CurrentHumanIdleSheets_AreShippedWithFourNativeFrames(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "art", "animations", fileName);
        Assert.True(File.Exists(path), $"Missing revised idle sheet: {path}");
        var png = File.ReadAllBytes(path);
        Assert.Equal(256, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(80, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    [Theory]
    [InlineData("unit.flora.sprout.actions.png")]
    [InlineData("unit.flora.thorn-guard.actions.png")]
    [InlineData("unit.flora.ember-bloom.actions.png")]
    [InlineData("unit.flora.pod-slinger.actions.png")]
    [InlineData("unit.flora.root-warden.actions.png")]
    public void FloraActionSpriteSheet_IsCopiedToGameContent(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "art", "animations", fileName);

        Assert.True(File.Exists(path), $"Expected action sprite sheet at {path}");
    }
}
