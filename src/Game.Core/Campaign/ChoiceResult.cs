namespace Game.Core.Campaigns;

public sealed record ChoiceResult(bool Success, string Message, QuestChoice? Choice = null);
