namespace Game.Core.Content;

using Game.Core.Combat;

/// <summary>Balance values and replaceable art key for one stable troop type.</summary>
public sealed record UnitDefinition(
    string Id,
    string FactionId,
    string Name,
    int HitPoints,
    int Damage,
    int Speed,
    int Movement,
    int Range,
    bool IsRanged,
    int RecruitmentCost,
    string ArtKey,
    UnitAbility Ability = UnitAbility.None);
