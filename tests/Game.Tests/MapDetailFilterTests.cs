using Game.MonoGame;
using Game.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests;

public sealed class MapDetailFilterTests
{
    [Fact]
    public void InternalTileJoinUsesPixelsFromTheNeighbouringTile()
    {
        var dark = new Color(80, 0, 0, 255);
        var light = new Color(160, 0, 0, 255);
        Color[][] tiles = [Enumerable.Repeat(dark, 16).ToArray(), Enumerable.Repeat(light, 16).ToArray(),
            Enumerable.Repeat(dark, 16).ToArray(), Enumerable.Repeat(light, 16).ToArray()];
        MapDetailFilter.ApplyTiles(tiles, 4, 4, "sharpen");
        Assert.InRange(tiles[0][7].R, 64, 79);
        Assert.InRange(tiles[1][4].R, 161, 176);
    }

    [Theory]
    [InlineData("sharpen")]
    [InlineData("pixel")]
    public void FlatColoursAndAlphaStayUnchanged(string style)
    {
        var colour = new Color(80, 120, 160, 200);
        var pixels = Enumerable.Repeat(colour, 64).ToArray();
        MapDetailFilter.Apply(pixels, 8, 8, style);
        Assert.All(pixels, pixel => Assert.Equal(colour, pixel));
    }

    [Fact]
    public void SharpeningIncreasesAnEdgeWithoutLargeHalos()
    {
        var pixels = Enumerable.Range(0, 64)
            .Select(i => new Color(i % 8 < 4 ? 80 : 160, 0, 0, 255)).ToArray();
        MapDetailFilter.Apply(pixels, 8, 8, "sharpen");
        Assert.InRange(pixels[3].R, 64, 79);
        Assert.InRange(pixels[4].R, 161, 176);
        Assert.All(pixels, pixel => Assert.Equal(255, pixel.A));
    }

    [Fact]
    public void PixelStyleUsesGameSizedBlocksAndKeepsPremultipliedAlpha()
    {
        var pixels = Enumerable.Range(0, 64)
            .Select(i => new Color(i * 2, i, 0, 140)).ToArray();
        MapDetailFilter.Apply(pixels, 8, 8, "pixel");
        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[0], pixels[8]);
        Assert.All(pixels, pixel => {
            Assert.Equal(140, pixel.A);
            Assert.True(pixel.R <= pixel.A && pixel.G <= pixel.A && pixel.B <= pixel.A);
        });
    }

    [Theory]
    [InlineData("sharpen")]
    [InlineData("pixel")]
    public void DetailModeIsExplicitAndCannotCombineWithOldPixelFilter(string style)
    {
        Assert.Equal(style, GameLaunchOptions.Parse(["--map-detail", style]).MapDetailStyle);
        Assert.Equal("original", GameLaunchOptions.Parse([]).MapDetailStyle);
        Assert.Throws<ArgumentException>(() => GameLaunchOptions.Parse(["--map-detail", style, "--map-pixels", "1"]));
        Assert.Throws<ArgumentException>(() => GameLaunchOptions.Parse(["--map-detail", style, "--map-detail", style]));
    }
}
