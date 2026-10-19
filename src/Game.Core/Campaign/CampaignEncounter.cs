using Game.Core.Characters;

namespace Game.Core.Campaigns;

/// <summary>An immutable encounter definition attached to an existing world point.</summary>
public sealed class CampaignEncounter
{
    public string Id { get; }
    public string PointOfInterestId { get; }
    public string Name { get; }
    public int Seed { get; }
    public IReadOnlyList<EncounterTroop> EnemyArmy { get; }
    public int GoldReward { get; }
    public int ExperienceReward { get; }

    public CampaignEncounter(string id, string pointOfInterestId, string name, int seed,
        IEnumerable<EncounterTroop> enemyArmy, int goldReward, int experienceReward)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(pointOfInterestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(enemyArmy);
        ArgumentOutOfRangeException.ThrowIfNegative(goldReward);
        ArgumentOutOfRangeException.ThrowIfNegative(experienceReward);
        var troops = enemyArmy.ToArray();
        if (troops.Length is < 1 or > Army.MaximumStacks || troops.Any(troop => troop is null))
            throw new ArgumentException("An encounter must contain between one and five enemy stacks.", nameof(enemyArmy));
        if (troops.Select(troop => troop.UnitTypeId).Distinct(StringComparer.Ordinal).Count() != troops.Length)
            throw new ArgumentException("An encounter cannot contain duplicate enemy unit types.", nameof(enemyArmy));
        Id = id;
        PointOfInterestId = pointOfInterestId;
        Name = name;
        Seed = seed;
        EnemyArmy = Array.AsReadOnly(troops);
        GoldReward = goldReward;
        ExperienceReward = experienceReward;
    }
}

public sealed record EncounterTroop
{
    public string UnitTypeId { get; }
    public int Count { get; }

    public EncounterTroop(string unitTypeId, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitTypeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        UnitTypeId = unitTypeId;
        Count = count;
    }
}
