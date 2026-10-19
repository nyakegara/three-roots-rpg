using System.Globalization;

namespace Game.MonoGame;

/// <summary>Launch settings shared by interactive previews and reproducible screenshots.</summary>
public sealed record GameLaunchOptions
{
    public string? ScreenshotPath { get; init; }
    public bool BattlePreview { get; init; }
    public bool PreviewDescent { get; init; }
    public bool PreviewQuestHover { get; init; }
    public string? PreviewRecruitmentSiteId { get; init; }
    public int BattleSeed { get; init; } = 1729;
    public bool Night { get; init; }

    public static GameLaunchOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? mode = null;
        string? screenshot = null;
        var seed = 1729;
        var hasSeed = false;
        var night = false;
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--seed":
                    if (hasSeed || ++index >= args.Length ||
                        !int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                        throw new ArgumentException("--seed requires one 32-bit integer and must not be repeated.");
                    hasSeed = true;
                    break;
                case "--night":
                    if (night) throw new ArgumentException("--night must not be repeated.");
                    night = true;
                    break;
                case "--battle":
                    SetMode(argument);
                    break;
                case "--screenshot":
                case "--screenshot-quest-hover":
                case "--screenshot-grid":
                case "--screenshot-grid-descent":
                case "--screenshot-recruitment":
                case "--screenshot-recruitment-sun":
                case "--screenshot-recruitment-spore":
                    SetMode(argument);
                    if (++index >= args.Length || !Path.IsPathFullyQualified(args[index]))
                        throw new ArgumentException(argument + " requires an absolute PNG output path.");
                    screenshot = Path.GetFullPath(args[index]);
                    break;
                default:
                    throw new ArgumentException("Unknown launch option: " + argument);
            }
        }

        var battle = mode is "--battle" or "--screenshot-grid" or "--screenshot-grid-descent";
        if ((hasSeed || night) && !battle)
            throw new ArgumentException("--seed and --night require a battle preview.");
        if (hasSeed && mode == "--screenshot-grid-descent")
            throw new ArgumentException("The fixed descent fixture does not accept a generated seed.");

        return new GameLaunchOptions
        {
            ScreenshotPath = screenshot,
            BattlePreview = battle,
            PreviewDescent = mode == "--screenshot-grid-descent",
            PreviewQuestHover = mode == "--screenshot-quest-hover",
            PreviewRecruitmentSiteId = mode switch
            {
                "--screenshot-recruitment" => "root-greenhouse",
                "--screenshot-recruitment-sun" => "sunny-clearing",
                "--screenshot-recruitment-spore" => "spore-circle",
                _ => null
            },
            BattleSeed = seed,
            Night = night
        };

        void SetMode(string requested)
        {
            if (mode is not null) throw new ArgumentException("Choose one scene or screenshot mode.");
            mode = requested;
        }
    }
}
