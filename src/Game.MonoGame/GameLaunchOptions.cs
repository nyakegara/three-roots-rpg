using System.Globalization;

namespace Game.MonoGame;

public enum BattleCaptureStage { Opening, Healing, Result, Arrow, Magic, ArrowImpact, MagicImpact }
public enum CampaignCaptureStage { None, EmptyEncounter, ReadyEncounter, Combat, Result, Returned }

/// <summary>Launch settings shared by interactive previews and reproducible screenshots.</summary>
public sealed record GameLaunchOptions
{
    public string? ScreenshotPath { get; init; }
    public bool BattlePreview { get; init; }
    public bool PlayableCombat { get; init; }
    public BattleCaptureStage CombatCapture { get; init; }
    public CampaignCaptureStage CampaignCapture { get; init; }
    public bool PreviewDescent { get; init; }
    public bool PreviewQuestHover { get; init; }
    public bool PreviewNorthTrail { get; init; }
    public bool PreviewIdleMap { get; init; }
    public bool PreviewMapPanel { get; init; }
    public bool PreviewJournalPanel { get; init; }
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
                case "--combat":
                    SetMode(argument);
                    break;
                case "--screenshot":
                case "--screenshot-quest-hover":
                case "--screenshot-north-trail":
                case "--screenshot-idle-map":
                case "--screenshot-map":
                case "--screenshot-journal":
                case "--screenshot-encounter-empty":
                case "--screenshot-encounter":
                case "--screenshot-campaign-battle":
                case "--screenshot-campaign-result":
                case "--screenshot-after-encounter":
                case "--screenshot-grid":
                case "--screenshot-combat":
                case "--screenshot-combat-night":
                case "--screenshot-combat-heal":
                case "--screenshot-combat-result":
                case "--screenshot-combat-arrow":
                case "--screenshot-combat-magic":
                case "--screenshot-combat-arrow-impact":
                case "--screenshot-combat-magic-impact":
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
        var combat = mode is "--combat" or "--screenshot-combat" or "--screenshot-combat-night"
            or "--screenshot-combat-heal" or "--screenshot-combat-result"
            or "--screenshot-combat-arrow" or "--screenshot-combat-magic"
            or "--screenshot-combat-arrow-impact" or "--screenshot-combat-magic-impact";
        if ((hasSeed || night) && !battle && !combat)
            throw new ArgumentException("--seed and --night require --battle, --combat or their screenshot modes.");
        if (hasSeed && mode == "--screenshot-grid-descent")
            throw new ArgumentException("The fixed descent fixture does not accept a generated seed.");

        return new GameLaunchOptions
        {
            ScreenshotPath = screenshot,
            BattlePreview = battle,
            PlayableCombat = combat,
            CampaignCapture = mode switch
            {
                "--screenshot-encounter-empty" => CampaignCaptureStage.EmptyEncounter,
                "--screenshot-encounter" => CampaignCaptureStage.ReadyEncounter,
                "--screenshot-campaign-battle" => CampaignCaptureStage.Combat,
                "--screenshot-campaign-result" => CampaignCaptureStage.Result,
                "--screenshot-after-encounter" => CampaignCaptureStage.Returned,
                _ => CampaignCaptureStage.None
            },
            CombatCapture = mode switch
            {
                "--screenshot-combat-heal" => BattleCaptureStage.Healing,
                "--screenshot-combat-result" => BattleCaptureStage.Result,
                "--screenshot-combat-arrow" => BattleCaptureStage.Arrow,
                "--screenshot-combat-magic" => BattleCaptureStage.Magic,
                "--screenshot-combat-arrow-impact" => BattleCaptureStage.ArrowImpact,
                "--screenshot-combat-magic-impact" => BattleCaptureStage.MagicImpact,
                _ => BattleCaptureStage.Opening
            },
            PreviewDescent = mode == "--screenshot-grid-descent",
            PreviewQuestHover = mode == "--screenshot-quest-hover",
            PreviewNorthTrail = mode == "--screenshot-north-trail",
            PreviewIdleMap = mode == "--screenshot-idle-map",
            PreviewMapPanel = mode == "--screenshot-map",
            PreviewJournalPanel = mode == "--screenshot-journal",
            PreviewRecruitmentSiteId = mode switch
            {
                "--screenshot-recruitment" => "starting-clearing",
                "--screenshot-recruitment-sun" => "sunny-clearing",
                "--screenshot-recruitment-spore" => "spore-circle",
                _ => null
            },
            BattleSeed = seed,
            Night = night || mode == "--screenshot-combat-night"
        };

        void SetMode(string requested)
        {
            if (mode is not null) throw new ArgumentException("Choose one scene or screenshot mode.");
            mode = requested;
        }
    }
}
