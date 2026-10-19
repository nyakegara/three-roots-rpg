namespace Game.Core.Campaigns;

public sealed class QuestChoice
{
    public string Id { get; }
    public string Description { get; }
    public int GoldChange { get; }
    public int ReputationChange { get; }

    public QuestChoice(string id, string description, int goldChange, int reputationChange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Id = id;
        Description = description;
        GoldChange = goldChange;
        ReputationChange = reputationChange;
    }
}
