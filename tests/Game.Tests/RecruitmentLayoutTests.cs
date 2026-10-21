using Game.MonoGame.Screens;
using Xunit;

namespace Game.Tests;

public sealed class RecruitmentLayoutTests
{
    [Theory]
    [InlineData(810, 358, -1)]
    [InlineData(934, 358, 1)]
    public void QuantityDeltaAt_UsesVisibleQuantityButtonCoordinates(int x, int y, int expectedDelta)
    {
        Assert.Equal(expectedDelta, RecruitmentLayout.QuantityDeltaAt(x, y));
    }
}
