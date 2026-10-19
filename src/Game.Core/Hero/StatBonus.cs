namespace Game.Core.Characters;

/// <summary>Small permanent bonuses that can come from a learned skill or an artifact.</summary>
public readonly record struct StatBonus(int Attack = 0, int Defense = 0, int MaxHitPoints = 0);
