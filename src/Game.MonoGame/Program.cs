namespace Game.MonoGame;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string? screenshotPath = null;
        var showGrid = true;
        var previewDescent = false;
        if (args.Length != 0)
        {
            if (args.Length != 2 || args[0] is not
                ("--screenshot" or "--screenshot-grid" or "--screenshot-grid-descent") ||
                !Path.IsPathFullyQualified(args[1]))
            {
                throw new ArgumentException(
                    "Usage: Game.MonoGame [--screenshot | --screenshot-grid | " +
                    "--screenshot-grid-descent] <absolute PNG path>");
            }

            screenshotPath = Path.GetFullPath(args[1]);
            previewDescent = args[0] == "--screenshot-grid-descent";
        }

        using var game = new Game1(screenshotPath, showGrid, previewDescent);
        game.Run();
    }
}
