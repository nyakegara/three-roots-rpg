using Game.MonoGame;
using Xunit;

namespace Game.Tests;

public sealed class GameLaunchOptionsTests
{
    [Theory]
    [InlineData("--screenshot-encounter-empty", CampaignCaptureStage.EmptyEncounter)]
    [InlineData("--screenshot-encounter", CampaignCaptureStage.ReadyEncounter)]
    [InlineData("--screenshot-campaign-battle", CampaignCaptureStage.Combat)]
    [InlineData("--screenshot-campaign-result", CampaignCaptureStage.Result)]
    [InlineData("--screenshot-after-encounter", CampaignCaptureStage.Returned)]
    public void CampaignCaptureRemainsExplorationMode(string mode, CampaignCaptureStage stage)
    {
        var options = GameLaunchOptions.Parse([mode, Path.Combine(Path.GetTempPath(), "campaign.png")]);
        Assert.Equal(stage, options.CampaignCapture);
        Assert.False(options.PlayableCombat);
        Assert.False(options.BattlePreview);
    }

    [Theory]
    [InlineData("--combat")]
    [InlineData("--screenshot-combat")]
    [InlineData("--screenshot-combat-night")]
    [InlineData("--screenshot-combat-heal")]
    [InlineData("--screenshot-combat-result")]
    [InlineData("--screenshot-combat-arrow")]
    [InlineData("--screenshot-combat-magic")]
    [InlineData("--screenshot-combat-holy-fire")]
    [InlineData("--screenshot-combat-blocked")]
    [InlineData("--screenshot-combat-after-attack")]
    [InlineData("--screenshot-combat-sprite-target")]
    [InlineData("--screenshot-combat-arrow-impact")]
    [InlineData("--screenshot-combat-magic-impact")]
    [InlineData("--screenshot-combat-melee")]
    [InlineData("--screenshot-combat-range")]
    [InlineData("--screenshot-combat-ranged-target")]
    public void PlayableCombatIsDistinctFromTerrainPreview(string mode)
    {
        var args = mode == "--combat" ? new[] { mode, "--seed", "42" }
            : new[] { mode, Path.Combine(Path.GetTempPath(), "combat.png"), "--seed", "42" };
        var options = GameLaunchOptions.Parse(args);
        Assert.True(options.PlayableCombat);
        Assert.False(options.BattlePreview);
        Assert.Equal(42, options.BattleSeed);
        Assert.Equal(mode.EndsWith("-night"), options.Night);
        Assert.Equal(mode.EndsWith("-heal") ? BattleCaptureStage.Healing :
            mode.EndsWith("-result") ? BattleCaptureStage.Result :
            mode.EndsWith("-arrow-impact") ? BattleCaptureStage.ArrowImpact :
            mode.EndsWith("-magic-impact") ? BattleCaptureStage.MagicImpact :
            mode.EndsWith("-melee") ? BattleCaptureStage.Melee :
            mode.EndsWith("-range") ? BattleCaptureStage.Range :
            mode.EndsWith("-ranged-target") ? BattleCaptureStage.RangedTarget :
            mode.EndsWith("-arrow") ? BattleCaptureStage.Arrow :
            mode.EndsWith("-magic") ? BattleCaptureStage.Magic :
            mode.EndsWith("-holy-fire") ? BattleCaptureStage.HolyFire :
            mode.EndsWith("-blocked") ? BattleCaptureStage.BlockedShot :
            mode.EndsWith("-after-attack") ? BattleCaptureStage.AfterAttack :
            mode.EndsWith("-sprite-target") ? BattleCaptureStage.SpriteTarget : BattleCaptureStage.Opening,
            options.CombatCapture);
    }

    [Fact]
    public void HolyFireScreenshot_SelectsSpecializedPosePreview()
    {
        var options = GameLaunchOptions.Parse(["--screenshot-combat-holy-fire", Path.Combine(Path.GetTempPath(), "holy-fire.png")]);
        Assert.Equal(BattleCaptureStage.HolyFire,
            options.CombatCapture);
    }

    [Fact]
    public void NoArgumentsKeepsExplorationAsDefault()
    {
        var options = GameLaunchOptions.Parse([]);
        Assert.False(options.BattlePreview);
        Assert.Null(options.ScreenshotPath);
        Assert.False(options.Night);
    }

    [Fact]
    public void InteractiveBattleAcceptsNightAndExtremeSeedInAnyOrder()
    {
        var options = GameLaunchOptions.Parse(["--night", "--seed", "-2147483648", "--battle"]);
        Assert.True(options.BattlePreview);
        Assert.True(options.Night);
        Assert.Equal(int.MinValue, options.BattleSeed);
        Assert.Null(options.ScreenshotPath);
    }

    [Fact]
    public void BattleScreenshotKeepsSeedAndAbsoluteOutputPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "battle-preview.png");
        var options = GameLaunchOptions.Parse(["--screenshot-grid", path, "--seed", "2147483647", "--night"]);
        Assert.Equal(Path.GetFullPath(path), options.ScreenshotPath);
        Assert.True(options.BattlePreview);
        Assert.Equal(int.MaxValue, options.BattleSeed);
        Assert.True(options.Night);
    }

    [Theory]
    [InlineData("--screenshot", false, false, null)]
    [InlineData("--screenshot-quest-hover", false, true, null)]
    [InlineData("--screenshot-north-trail", false, false, null)]
    [InlineData("--screenshot-recruitment", false, false, "starting-clearing")]
    [InlineData("--screenshot-recruitment-sun", false, false, "sunny-clearing")]
    [InlineData("--screenshot-recruitment-spore", false, false, "spore-circle")]
    [InlineData("--screenshot-grid-descent", true, false, null)]
    public void ExistingScreenshotModesRetainTheirScene(string mode, bool battle, bool hover, string? site)
    {
        var options = GameLaunchOptions.Parse([mode, Path.Combine(Path.GetTempPath(), "preview.png")]);
        Assert.Equal(battle, options.BattlePreview);
        Assert.Equal(hover, options.PreviewQuestHover);
        Assert.Equal(site, options.PreviewRecruitmentSiteId);
        Assert.Equal(mode == "--screenshot-grid-descent", options.PreviewDescent);
    }

    [Fact]
    public void NorthernTrailScreenshotSelectsThePassage()
    {
        var options = GameLaunchOptions.Parse(["--screenshot-north-trail", Path.Combine(Path.GetTempPath(), "trail.png")]);
        Assert.True(options.PreviewNorthTrail);
    }

    [Theory]
    [InlineData("--screenshot-map", true, false)]
    [InlineData("--screenshot-journal", false, true)]
    public void ExplorationPanelScreenshotOpensRequestedPanel(string mode, bool map, bool journal)
    {
        var options = GameLaunchOptions.Parse([mode, Path.Combine(Path.GetTempPath(), "panel.png")]);
        Assert.True(options.PreviewMapPanel == map);
        Assert.True(options.PreviewJournalPanel == journal);
        Assert.False(options.PlayableCombat);
    }

    [Theory]
    [InlineData("--seed")]
    [InlineData("--night")]
    [InlineData("--unknown")]
    [InlineData("--battle", "--seed")]
    [InlineData("--battle", "--seed", "2147483648")]
    [InlineData("--battle", "--seed", "abc")]
    [InlineData("--battle", "--night", "--night")]
    [InlineData("--battle", "--seed", "1", "--seed", "2")]
    [InlineData("--battle", "--battle")]
    [InlineData("--screenshot", "relative.png")]
    public void InvalidOrAmbiguousArgumentsAreRejected(params string[] args) =>
        Assert.Throws<ArgumentException>(() => GameLaunchOptions.Parse(args));

    [Fact]
    public void BattleOnlyOptionsDoNotSilentlyChangeExploration()
    {
        var path = Path.Combine(Path.GetTempPath(), "preview.png");
        Assert.Throws<ArgumentException>(() => GameLaunchOptions.Parse(["--screenshot", path, "--night"]));
        Assert.Throws<ArgumentException>(() => GameLaunchOptions.Parse(["--screenshot-recruitment", path, "--seed", "42"]));
        Assert.Throws<ArgumentException>(() => GameLaunchOptions.Parse(["--screenshot-grid-descent", path, "--seed", "42"]));
    }
}
