namespace Game.Core.Campaigns;

/// <summary>Conditions attached to a campaign action. Empty conditions allow it immediately.</summary>
public sealed record CampaignAccessRequirement(
    IReadOnlyList<string> RequiredEncounterIds,
    string? QuestId = null,
    string? ChoiceId = null);

/// <summary>Content-defined progression; completed encounters are the source of truth for unlocks.</summary>
public sealed class CampaignProgressionRules
{
    public IReadOnlyDictionary<string, CampaignAccessRequirement> EncounterRequirements { get; }
    public IReadOnlyDictionary<string, CampaignAccessRequirement> RecruitmentRequirements { get; }
    public IReadOnlyDictionary<string, string> UnlockEncounterIds { get; }
    public IReadOnlyDictionary<string, int> EncounterReputationRewards { get; }
    public string? VictoryEncounterId { get; }
    public bool DefeatOnArmyWipe { get; }

    public CampaignProgressionRules(
        IReadOnlyDictionary<string, CampaignAccessRequirement>? encounterRequirements = null,
        IReadOnlyDictionary<string, CampaignAccessRequirement>? recruitmentRequirements = null,
        IReadOnlyDictionary<string, string>? unlockEncounterIds = null,
        IReadOnlyDictionary<string, int>? encounterReputationRewards = null,
        string? victoryEncounterId = null,
        bool defeatOnArmyWipe = false)
    {
        EncounterRequirements = CopyRequirements(encounterRequirements);
        RecruitmentRequirements = CopyRequirements(recruitmentRequirements);
        UnlockEncounterIds = Copy(unlockEncounterIds);
        EncounterReputationRewards = encounterReputationRewards is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(encounterReputationRewards, StringComparer.Ordinal);
        VictoryEncounterId = victoryEncounterId;
        DefeatOnArmyWipe = defeatOnArmyWipe;
    }

    private static IReadOnlyDictionary<string, CampaignAccessRequirement> CopyRequirements(
        IReadOnlyDictionary<string, CampaignAccessRequirement>? source)
    {
        var copy = new Dictionary<string, CampaignAccessRequirement>(StringComparer.Ordinal);
        foreach (var (id, requirement) in source ?? new Dictionary<string, CampaignAccessRequirement>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNull(requirement);
            if (requirement.RequiredEncounterIds is null ||
                requirement.RequiredEncounterIds.Any(string.IsNullOrWhiteSpace) ||
                (requirement.ChoiceId is not null && string.IsNullOrWhiteSpace(requirement.QuestId)))
                throw new ArgumentException("Invalid campaign access requirement.", nameof(source));
            copy.Add(id, new CampaignAccessRequirement(requirement.RequiredEncounterIds.ToArray(),
                requirement.QuestId, requirement.ChoiceId));
        }
        return copy;
    }

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string>? source)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, encounterId) in source ?? new Dictionary<string, string>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
            copy.Add(id, encounterId);
        }
        return copy;
    }
}
