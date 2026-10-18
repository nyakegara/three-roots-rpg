namespace Game.MonoGame;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string? screenshotPath = null;
        var showGrid = false;
        if (args.Length != 0)
        {
            if (args.Length != 2 || args[0] is not ("--screenshot" or "--screenshot-grid") ||
                !Path.IsPathFullyQualified(args[1]))
            {
                throw new ArgumentException(
                    "Usage: Game.MonoGame [--screenshot | --screenshot-grid] <absolute PNG path>");
            }

            screenshotPath = Path.GetFullPath(args[1]);
            showGrid = args[0] == "--screenshot-grid";
        }

        using var game = new Game1(screenshotPath, showGrid);
        game.Run();
    }
}
