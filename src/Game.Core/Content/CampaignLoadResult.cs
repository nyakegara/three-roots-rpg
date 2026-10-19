namespace Game.Core.Content;

public sealed record CampaignLoadResult(
    bool Success,
    CampaignDefinition? Definition,
    IReadOnlyList<ContentError> Errors);
