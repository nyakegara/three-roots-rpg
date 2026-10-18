namespace Game.MonoGame;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string? screenshotPath = null;
        if (args.Length != 0)
        {
            if (args.Length != 2 || args[0] != "--screenshot" ||
                !Path.IsPathFullyQualified(args[1]))
            {
                throw new ArgumentException("Usage: Game.MonoGame --screenshot <absolute PNG path>");
            }

            screenshotPath = Path.GetFullPath(args[1]);
        }

        using var game = new Game1(screenshotPath);
        game.Run();
    }
}
