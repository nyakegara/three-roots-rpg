using System.Globalization;

namespace Game.MonoGame;

public enum BattleCaptureStage { Opening, Healing, Result, Arrow, Magic, ArrowImpact, MagicImpact, Melee, Range, Target, RangedTarget, HolyFire, BlockedShot, AfterAttack, SpriteTarget, Initiative, HunterAttack, HunterRest, ActionLock, Outpost, Undead }
public enum CampaignCaptureStage { None, EmptyEncounter, ReadyEncounter, Combat, Result, Returned, AfterChoice }

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
    public bool PreviewNavigation { get; init; }
    public bool PreviewFreeTravel { get; init; }
    public string? PreviewRecruitmentSiteId { get; init; }
    public string? PreviewMapSiteId { get; init; }
    public int BattleSeed { get; init; } = 1729;
    public bool Night { get; init; }
    public int MapPixelSize { get; init; }
    public string MapDetailStyle { get; init; } = "original";

    public static GameLaunchOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? mode = null;
        string? screenshot = null;
        var seed = 1729;
        var hasSeed = false;
        var night = false;
        var mapPixelSize = 0;
        var mapDetailStyle = "original";
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--map-detail":
                    if (mapDetailStyle != "original" || ++index >= args.Length ||
                        args[index] is not ("sharpen" or "pixel"))
                        throw new ArgumentException("--map-detail requires sharpen or pixel and must not be repeated.");
                    mapDetailStyle = args[index];
                    break;
                case "--map-pixels":
                    if (mapPixelSize != 0 || ++index >= args.Length ||
                        !int.TryParse(args[index], out mapPixelSize) || mapPixelSize is < 1 or > 2)
                        throw new ArgumentException("--map-pixels requires 1 or 2 and must not be repeated.");
                    break;
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
                case "--combat-undead":
                    SetMode(argument);
                    break;
                case "--screenshot-site-t1":
                case "--screenshot-site-t2":
                case "--screenshot-site-t3":
                case "--screenshot-site-peasants":
                case "--screenshot":
                case "--screenshot-quest-hover":
                case "--screenshot-north-trail":
                case "--screenshot-idle-map":
                case "--screenshot-map":
                case "--screenshot-navigation":
                case "--screenshot-travel":
                case "--screenshot-journal":
                case "--screenshot-encounter-empty":
                case "--screenshot-encounter":
                case "--screenshot-campaign-battle":
                case "--screenshot-campaign-result":
                case "--screenshot-after-encounter":
                case "--screenshot-after-choice":
                case "--screenshot-map-after-choice":
                case "--screenshot-grid":
                case "--screenshot-combat-outpost":
                case "--screenshot-combat-undead":
                case "--screenshot-combat":
                case "--screenshot-combat-night":
                case "--screenshot-combat-heal":
                case "--screenshot-combat-result":
                case "--screenshot-combat-arrow":
                case "--screenshot-combat-magic":
                case "--screenshot-combat-holy-fire":
                case "--screenshot-combat-blocked":
                case "--screenshot-combat-after-attack":
                case "--screenshot-combat-sprite-target":
                case "--screenshot-combat-initiative":
                case "--screenshot-combat-action":
                case "--screenshot-combat-hunter":
                case "--screenshot-combat-hunter-rest":
                case "--screenshot-combat-arrow-impact":
                case "--screenshot-combat-magic-impact":
                case "--screenshot-combat-melee":
                case "--screenshot-combat-range":
                case "--screenshot-combat-target":
                case "--screenshot-combat-ranged-target":
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
        if (mapPixelSize > 0 && mapDetailStyle != "original")
            throw new ArgumentException("Choose --map-pixels or --map-detail, not both.");
        var combat = mode is "--screenshot-combat-outpost" or "--screenshot-combat-undead" or "--combat" or "--combat-undead" or "--screenshot-combat" or "--screenshot-combat-night"
            or "--screenshot-combat-heal" or "--screenshot-combat-result"
            or "--screenshot-combat-arrow" or "--screenshot-combat-magic"
            or "--screenshot-combat-holy-fire"
            or "--screenshot-combat-blocked" or "--screenshot-combat-after-attack"
            or "--screenshot-combat-sprite-target"
            or "--screenshot-combat-initiative"
            or "--screenshot-combat-action"
            or "--screenshot-combat-hunter" or "--screenshot-combat-hunter-rest"
            or "--screenshot-combat-arrow-impact" or "--screenshot-combat-magic-impact"
            or "--screenshot-combat-melee"
            or "--screenshot-combat-range" or "--screenshot-combat-target" or "--screenshot-combat-ranged-target";
        if ((hasSeed || night) && !battle && !combat && !(night && !hasSeed && mode == "--screenshot-idle-map"))
            throw new ArgumentException("--seed and --night require --battle, --combat or their screenshot modes.");
        if (hasSeed && mode == "--screenshot-grid-descent")
            throw new ArgumentException("The fixed descent fixture does not accept a generated seed.");

        return new GameLaunchOptions
        {
            ScreenshotPath = screenshot,
            MapPixelSize = mapPixelSize,
            MapDetailStyle = mapDetailStyle,
            BattlePreview = battle,
            PlayableCombat = combat,
            CampaignCapture = mode switch
            {
                "--screenshot-encounter-empty" => CampaignCaptureStage.EmptyEncounter,
                "--screenshot-encounter" => CampaignCaptureStage.ReadyEncounter,
                "--screenshot-campaign-battle" => CampaignCaptureStage.Combat,
                "--screenshot-campaign-result" => CampaignCaptureStage.Result,
                "--screenshot-after-encounter" => CampaignCaptureStage.Returned,
                "--screenshot-after-choice" or "--screenshot-map-after-choice" => CampaignCaptureStage.AfterChoice,
                _ => CampaignCaptureStage.None
            },
            CombatCapture = mode switch
            {
                "--screenshot-combat-outpost" => BattleCaptureStage.Outpost,
                "--screenshot-combat-undead" or "--combat-undead" => BattleCaptureStage.Undead,
                "--screenshot-combat-heal" => BattleCaptureStage.Healing,
                "--screenshot-combat-result" => BattleCaptureStage.Result,
                "--screenshot-combat-arrow" => BattleCaptureStage.Arrow,
                "--screenshot-combat-magic" => BattleCaptureStage.Magic,
                "--screenshot-combat-holy-fire" => BattleCaptureStage.HolyFire,
                "--screenshot-combat-blocked" => BattleCaptureStage.BlockedShot,
                "--screenshot-combat-after-attack" => BattleCaptureStage.AfterAttack,
                "--screenshot-combat-sprite-target" => BattleCaptureStage.SpriteTarget,
                "--screenshot-combat-initiative" => BattleCaptureStage.Initiative,
                "--screenshot-combat-action" => BattleCaptureStage.ActionLock,
                "--screenshot-combat-hunter" => BattleCaptureStage.HunterAttack,
                "--screenshot-combat-hunter-rest" => BattleCaptureStage.HunterRest,
                "--screenshot-combat-arrow-impact" => BattleCaptureStage.ArrowImpact,
                "--screenshot-combat-magic-impact" => BattleCaptureStage.MagicImpact,
                "--screenshot-combat-melee" => BattleCaptureStage.Melee,
                "--screenshot-combat-range" => BattleCaptureStage.Range,
                "--screenshot-combat-target" => BattleCaptureStage.Target,
                "--screenshot-combat-ranged-target" => BattleCaptureStage.RangedTarget,
                _ => BattleCaptureStage.Opening
            },
            PreviewDescent = mode == "--screenshot-grid-descent",
            PreviewQuestHover = mode == "--screenshot-quest-hover",
            PreviewNorthTrail = mode == "--screenshot-north-trail",
            PreviewIdleMap = mode == "--screenshot-idle-map",
            PreviewMapPanel = mode is "--screenshot-map" or "--screenshot-navigation" or "--screenshot-map-after-choice",
            PreviewNavigation = mode == "--screenshot-navigation",
            PreviewFreeTravel = mode == "--screenshot-travel",
            PreviewJournalPanel = mode == "--screenshot-journal",
            PreviewMapSiteId = mode switch
            {
                "--screenshot-site-t1" => "starting-clearing",
                "--screenshot-site-t2" => "flora-tier-two",
                "--screenshot-site-t3" => "flora-tier-three",
                "--screenshot-site-peasants" => "peasant-recruitment",
                _ => null
            },
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
