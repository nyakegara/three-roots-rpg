using Game.Core.Campaigns;
using Game.Core.Characters;
using Game.Core.World;

namespace Game.Core.Content;

public sealed record CampaignRecruitment(string UnitId, int StartingStock, int UnitCost);

public sealed record CampaignRecruitmentOffer(string UnitId, int StartingStock, int UnitCost);

public sealed class CampaignRecruitmentSite
{
    public string Id { get; }
    public string PointOfInterestId { get; }
    public string FactionId { get; }
    public IReadOnlyList<CampaignRecruitmentOffer> Offers { get; }

    public CampaignRecruitmentSite(string id, string pointOfInterestId,
        IEnumerable<CampaignRecruitmentOffer> offers, string factionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(pointOfInterestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(factionId);
        ArgumentNullException.ThrowIfNull(offers);
        Id = id;
        PointOfInterestId = pointOfInterestId;
        FactionId = factionId;
        Offers = Array.AsReadOnly(offers.ToArray());
    }
}

public sealed record CampaignQuest(string PointOfInterestId, Quest Quest);

/// <summary>Immutable data needed to start one standalone mini-campaign.</summary>
public sealed class CampaignDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string PlayerFactionId { get; }
    public string EnemyFactionId { get; }
    public string MapFile { get; }
    public WorldMap Map { get; }
    public string HeroId { get; }
    public string HeroName { get; }
    public int StartingGold { get; }
    public IReadOnlyList<CampaignRecruitment> Recruitment { get; }
    public IReadOnlyList<CampaignRecruitmentSite> RecruitmentSites { get; }
    public IReadOnlyList<CampaignQuest> Quests { get; }
    public IReadOnlyList<CampaignEncounter> Encounters { get; }
    public IReadOnlyList<EncounterTroop> StartingArmy { get; }
    public CampaignProgressionRules Progression { get; }

    internal CampaignDefinition(string id, string name, string description, string playerFactionId,
        string enemyFactionId, string mapFile, WorldMap map, string heroId, string heroName,
        int startingGold, IEnumerable<CampaignRecruitment> recruitment,
        IEnumerable<CampaignRecruitmentSite> recruitmentSites, IEnumerable<CampaignQuest> quests,
        IEnumerable<CampaignEncounter> encounters, IEnumerable<EncounterTroop>? startingArmy = null,
        CampaignProgressionRules? progression = null)
    {
        Id = id;
        Name = name;
        Description = description;
        PlayerFactionId = playerFactionId;
        EnemyFactionId = enemyFactionId;
        MapFile = mapFile;
        Map = map;
        HeroId = heroId;
        HeroName = heroName;
        StartingGold = startingGold;
        Recruitment = Array.AsReadOnly(recruitment.ToArray());
        RecruitmentSites = Array.AsReadOnly(recruitmentSites.ToArray());
        Quests = Array.AsReadOnly(quests.ToArray());
        Encounters = Array.AsReadOnly(encounters.ToArray());
        StartingArmy = Array.AsReadOnly((startingArmy ?? []).ToArray());
        Progression = progression ?? new CampaignProgressionRules();
    }

    public Campaign CreateGameState()
    {
        var hero = new Hero(HeroId, HeroName, StartingGold, factionId: PlayerFactionId);
        foreach (var troop in StartingArmy)
            if (!hero.Army.TryAdd(troop.UnitTypeId, troop.Count))
                throw new InvalidOperationException($"Could not add starting army stack '{troop.UnitTypeId}'.");
        return new Campaign(hero, Quests.Select(entry => entry.Quest), RecruitmentSites.Select(site => new Game.Core.Economy.RecruitmentSite(
            site.Id, site.Offers.Select(offer => new Game.Core.Economy.RecruitmentOffer(
                offer.UnitId, offer.UnitCost, offer.StartingStock)), site.FactionId)), Encounters, Progression);
    }
}
