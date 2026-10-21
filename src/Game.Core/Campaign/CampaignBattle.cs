using Game.Core.Combat;
using Game.Core.Combat.Generation;

namespace Game.Core.Campaigns;

/// <summary>A tactical battle owned by the campaign that started it.</summary>
public sealed class CampaignBattle
{
    public CampaignEncounter Encounter { get; }
    public GeneratedBattlefield Field { get; }
    public Battle Battle { get; }
    internal Campaign Owner { get; }
    internal IReadOnlyList<EncounterTroop> StartingArmy { get; }

    internal CampaignBattle(Campaign owner, CampaignEncounter encounter, GeneratedBattlefield field,
        Battle battle, IReadOnlyList<EncounterTroop> startingArmy)
    {
        Owner = owner;
        Encounter = encounter;
        Field = field;
        Battle = battle;
        StartingArmy = startingArmy;
    }
}
