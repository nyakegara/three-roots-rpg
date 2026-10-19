namespace Game.Core.Economy;

public sealed record RecruitmentResult(bool Success, string Message, int UnitCost = 0, int TotalCost = 0);
