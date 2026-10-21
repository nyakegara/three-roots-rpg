using Game.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests;

public sealed class MapPixelFilterTests
{
    [Fact]
    public void PaletteRoundingKeepsTranslucentPixelsPremultiplied()
    {
        var pixels = Enumerable.Repeat(new Color(5, 0, 0, 5), 16).ToArray();
        MapPixelFilter.Apply(pixels, 4, 4, 1);
        Assert.All(pixels, pixel => Assert.True(pixel.R <= pixel.A));
    }

    [Fact]
    public void BlocksUseOneColourWithoutChangingDimensionsOrAlpha()
    {
        var pixels = Enumerable.Range(0, 32).Select(i => new Color(i * 7, i * 3, i * 5, 255)).ToArray();
        MapPixelFilter.Apply(pixels, 8, 4, 2);
        Assert.Equal(32, pixels.Length);
        Assert.All(pixels.Take(4), pixel => Assert.Equal(pixels[0], pixel));
        Assert.All(pixels.Skip(8).Take(4), pixel => Assert.Equal(pixels[0], pixel));
        Assert.All(pixels, pixel => Assert.Equal(255, pixel.A));
        Assert.NotEqual(pixels[0], pixels[4]);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    public void LaunchOptionSelectsAnOptionalMapOnlyExperiment(string value, int expected)
    {
        Assert.Equal(expected, Game.MonoGame.GameLaunchOptions.Parse(["--map-pixels", value]).MapPixelSize);
        Assert.Equal(0, Game.MonoGame.GameLaunchOptions.Parse([]).MapPixelSize);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("bad")]
    public void InvalidPixelSizeIsRejected(string value) =>
        Assert.Throws<ArgumentException>(() => Game.MonoGame.GameLaunchOptions.Parse(["--map-pixels", value]));
}
