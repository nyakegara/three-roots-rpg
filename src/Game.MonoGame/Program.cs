namespace Game.MonoGame;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var options = GameLaunchOptions.Parse(args);
        using var game = new Game1(options.ScreenshotPath, options.BattlePreview, options.PreviewDescent,
            options.PreviewQuestHover, options.PreviewRecruitmentSiteId, options.BattleSeed, options.Night,
            options.PlayableCombat, options.CombatCapture);
        game.Run();
    }
}
