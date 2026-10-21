using Game.MonoGame;
using Xunit;

namespace Game.Tests;

public sealed class PixelFontTests
{
    [Theory]
    [InlineData('—')]
    [InlineData('–')]
    [InlineData('−')]
    public void DialogueDashes_UseTheExistingDashGlyph(char character)
    {
        Assert.NotNull(PixelFont.GetGlyph(character));
        Assert.Equal(PixelFont.GetGlyph('-'), PixelFont.GetGlyph(character));
    }

    [Fact]
    public void ExclamationMark_HasASevenRowGlyphWithSeparateDot()
    {
        var rows = Assert.IsAssignableFrom<IReadOnlyList<string>>(PixelFont.GetGlyph('!'));
        Assert.Equal(7, rows.Count);
        Assert.All(rows, row => Assert.Equal(5, row.Length));
        Assert.Equal("00000", rows[5]);
        Assert.Equal("00100", rows[6]);
    }

    [Theory]
    [InlineData('ё', 'Ё')]
    [InlineData('a', 'A')]
    public void GlyphLookup_PreservesUppercaseRendering(char lower, char upper) =>
        Assert.Equal(PixelFont.GetGlyph(upper), PixelFont.GetGlyph(lower));
}
