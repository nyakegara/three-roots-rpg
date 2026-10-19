namespace Game.Core.Campaigns;

public sealed record EncounterStartResult(bool Success, string Message, CampaignBattle? Session = null);

public sealed record EncounterResolution(bool Success, string Message, bool Victory = false,
    int GoldAwarded = 0, int ExperienceAwarded = 0);
