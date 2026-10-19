using Game.MonoGame.Rendering;
using Xunit;

namespace Game.Tests;

public sealed class RecruitmentPointArtTests
{
    [Theory]
    [InlineData("root-greenhouse", "poi.flora.root-greenhouse")]
    [InlineData("sunny-clearing", "poi.flora.sunny-clearing")]
    [InlineData("spore-circle", "poi.flora.spore-circle")]
    public void GetAssetKey_ReturnsArtForKnownRecruitmentSites(string siteId, string expectedKey)
    {
        Assert.Equal(expectedKey, RecruitmentPointArt.GetAssetKey(siteId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-site")]
    public void GetAssetKey_ReturnsNullForUnknownRecruitmentSites(string? siteId)
    {
        Assert.Null(RecruitmentPointArt.GetAssetKey(siteId));
    }
}
