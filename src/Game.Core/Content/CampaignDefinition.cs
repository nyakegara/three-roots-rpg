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
    public IReadOnlyList<CampaignRecruitmentOffer> Offers { get; }

    public CampaignRecruitmentSite(string id, string pointOfInterestId,
        IEnumerable<CampaignRecruitmentOffer> offers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(pointOfInterestId);
        ArgumentNullException.ThrowIfNull(offers);
        Id = id;
        PointOfInterestId = pointOfInterestId;
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

    internal CampaignDefinition(string id, string name, string description, string playerFactionId,
        string enemyFactionId, string mapFile, WorldMap map, string heroId, string heroName,
        int startingGold, IEnumerable<CampaignRecruitment> recruitment,
        IEnumerable<CampaignRecruitmentSite> recruitmentSites, IEnumerable<CampaignQuest> quests,
        IEnumerable<CampaignEncounter> encounters)
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
    }

    public Campaign CreateGameState() => new(new Hero(HeroId, HeroName, StartingGold),
        Quests.Select(entry => entry.Quest), RecruitmentSites.Select(site => new Game.Core.Economy.RecruitmentSite(
            site.Id, site.Offers.Select(offer => new Game.Core.Economy.RecruitmentOffer(
                offer.UnitId, offer.UnitCost, offer.StartingStock)))), Encounters);
}
