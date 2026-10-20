using Xunit;

namespace Game.Tests;

public sealed class FloraActionAssetsTests
{
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
