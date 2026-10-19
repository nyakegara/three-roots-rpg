using Game.Core.Geometry;

namespace Game.Core.Persistence;

/// <summary>Serializable campaign progress. Content assets are referenced by stable ids.</summary>
public sealed record CampaignSave(
    string CampaignId,
    string HeroId,
    string HeroName,
    string MapId,
    HexCoord HeroPosition,
    int Gold,
    int Reputation,
    int Experience,
    int Level,
    int SkillPoints,
    IReadOnlyList<SavedTroopStack> Army,
    IReadOnlyList<string> CompletedQuestIds,
    IReadOnlyList<SavedQuestChoice> SelectedChoices,
    IReadOnlyList<string> SkillIds,
    IReadOnlyList<string> ArtifactIds)
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public IReadOnlyList<SavedRecruitmentStock> RecruitmentStocks { get; init; } = [];
}

public sealed record SavedTroopStack(string UnitId, int Count);

public sealed record SavedQuestChoice(string QuestId, string ChoiceId);

public sealed record SavedRecruitmentStock(string SiteId, string UnitId, int RemainingStock);
