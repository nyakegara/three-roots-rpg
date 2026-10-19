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
        string? previewRecruitmentSiteId = null;
        if (args.Length != 0)
        {
            if (args.Length != 2 || args[0] is not
                ("--screenshot" or "--screenshot-quest-hover" or
                    "--screenshot-grid" or "--screenshot-grid-descent" or
                    "--screenshot-recruitment" or "--screenshot-recruitment-sun" or
                    "--screenshot-recruitment-spore") ||
                !Path.IsPathFullyQualified(args[1]))
            {
                throw new ArgumentException(
                    "Usage: Game.MonoGame [--screenshot | --screenshot-quest-hover | --screenshot-recruitment | " +
                    "--screenshot-recruitment-sun | --screenshot-recruitment-spore | " +
                    "--screenshot-grid | " +
                    "--screenshot-grid-descent] <absolute PNG path>");
            }

            screenshotPath = Path.GetFullPath(args[1]);
            battlePreview = args[0] is "--screenshot-grid" or "--screenshot-grid-descent";
            previewDescent = args[0] == "--screenshot-grid-descent";
            previewQuestHover = args[0] == "--screenshot-quest-hover";
            previewRecruitmentSiteId = args[0] switch
            {
                "--screenshot-recruitment" => "root-greenhouse",
                "--screenshot-recruitment-sun" => "sunny-clearing",
                "--screenshot-recruitment-spore" => "spore-circle",
                _ => null
            };
        }

        using var game = new Game1(screenshotPath, battlePreview, previewDescent,
            previewQuestHover, previewRecruitmentSiteId);
        game.Run();
    }
}
