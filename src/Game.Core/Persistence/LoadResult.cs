namespace Game.Core.Persistence;

public sealed record LoadResult(bool Success, CampaignSave? Save, string Message);
