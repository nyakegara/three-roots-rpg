namespace Game.MonoGame;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string? screenshotPath = null;
        var battlePreview = false;
        var previewDescent = false;
        var previewQuestHover = false;
        var previewRecruitment = false;
        if (args.Length != 0)
        {
            if (args.Length != 2 || args[0] is not
                ("--screenshot" or "--screenshot-quest-hover" or
                    "--screenshot-grid" or "--screenshot-grid-descent" or "--screenshot-recruitment") ||
                !Path.IsPathFullyQualified(args[1]))
            {
                throw new ArgumentException(
                    "Usage: Game.MonoGame [--screenshot | --screenshot-quest-hover | --screenshot-recruitment | " +
                    "--screenshot-grid | " +
                    "--screenshot-grid-descent] <absolute PNG path>");
            }

            screenshotPath = Path.GetFullPath(args[1]);
            battlePreview = args[0] is "--screenshot-grid" or "--screenshot-grid-descent";
            previewDescent = args[0] == "--screenshot-grid-descent";
            previewQuestHover = args[0] == "--screenshot-quest-hover";
            previewRecruitment = args[0] == "--screenshot-recruitment";
        }

        using var game = new Game1(screenshotPath, battlePreview, previewDescent,
            previewQuestHover, previewRecruitment);
        game.Run();
    }
}
