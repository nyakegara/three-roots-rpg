using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Campaigns;

namespace Game.Core.Content;

/// <summary>Loads one campaign definition and validates its links to map and unit content.</summary>
public sealed class CampaignLoader(UnitCatalog units, MapLoader? mapLoader = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly MapLoader _mapLoader = mapLoader ?? new MapLoader();

    public CampaignLoadResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Failed(path ?? string.Empty, "A campaign file path is required.");

        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { return Failed(path, $"Could not read campaign file: {exception.Message}"); }

        CampaignDocument? document;
        try { document = JsonSerializer.Deserialize<CampaignDocument>(json, JsonOptions); }
        catch (JsonException exception)
        { return Failed(path, $"Invalid JSON: {exception.Message}", exception.LineNumber + 1, exception.BytePositionInLine + 1); }
        if (document is null) return Failed(path, "Campaign JSON must contain an object.");

        var errors = new List<ContentError>();
        Require(document.Id, "id", path, errors);
        Require(document.Name, "name", path, errors);
        Require(document.Description, "description", path, errors);
        Require(document.PlayerFactionId, "playerFactionId", path, errors);
        Require(document.EnemyFactionId, "enemyFactionId", path, errors);
        Require(document.MapFile, "mapFile", path, errors);
        if (document.PlayerFactionId is not null && !units.Factions.Contains(document.PlayerFactionId, StringComparer.Ordinal))
            errors.Add(new ContentError(path, $"Unknown player faction '{document.PlayerFactionId}'."));
        if (document.EnemyFactionId is not null && !units.Factions.Contains(document.EnemyFactionId, StringComparer.Ordinal))
            errors.Add(new ContentError(path, $"Unknown enemy faction '{document.EnemyFactionId}'."));
        if (document.PlayerFactionId is not null && document.PlayerFactionId == document.EnemyFactionId)
            errors.Add(new ContentError(path, "Player and enemy factions must be different."));

        if (document.Hero is null)
        {
            errors.Add(new ContentError(path, "Required field 'hero' is missing."));
        }
        else
        {
            Require(document.Hero.Id, "hero.id", path, errors);
            Require(document.Hero.Name, "hero.name", path, errors);
            if (document.Hero.StartingGold is null or < 0)
                errors.Add(new ContentError(path, "'hero.startingGold' must be zero or a positive integer."));
        }

        var mapPath = string.Empty;
        Game.Core.World.WorldMap? map = null;
        if (!string.IsNullOrWhiteSpace(document.MapFile))
        {
            try
            {
                var campaignDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
                var mapDirectory = Path.GetFullPath(Path.Combine(campaignDirectory, "..", "maps"));
                mapPath = Path.GetFullPath(document.MapFile, campaignDirectory);
                var relativeMapPath = Path.GetRelativePath(mapDirectory, mapPath);
                if (relativeMapPath == ".." || relativeMapPath.StartsWith(".." + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal) || Path.IsPathRooted(relativeMapPath))
                {
                    errors.Add(new ContentError(path, "The 'mapFile' must resolve inside the sibling map directory."));
                    mapPath = string.Empty;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            { errors.Add(new ContentError(path, $"Invalid mapFile path: {exception.Message}")); }
            if (mapPath.Length > 0)
            {
                var mapResult = _mapLoader.Load(mapPath);
                errors.AddRange(mapResult.Errors);
                map = mapResult.Map;
            }
        }

        var recruitment = new List<CampaignRecruitment>();
        var recruitedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var offer in document.Recruitment ?? [])
        {
            if (offer is null || string.IsNullOrWhiteSpace(offer.UnitId) || offer.StartingStock is null or <= 0)
            {
                errors.Add(new ContentError(path, "Each recruitment entry must define unitId and positive startingStock."));
                continue;
            }
            if (!recruitedIds.Add(offer.UnitId))
            {
                errors.Add(new ContentError(path, $"Campaign contains duplicate recruitment unit '{offer.UnitId}'."));
                continue;
            }
            var definition = units.Get(offer.UnitId);
            if (definition is null)
            {
                errors.Add(new ContentError(path, $"Campaign references unknown unitId '{offer.UnitId}'."));
                continue;
            }
            if (document.PlayerFactionId is not null && definition.FactionId != document.PlayerFactionId)
            {
                errors.Add(new ContentError(path, $"Recruitable unit '{offer.UnitId}' does not belong to player faction '{document.PlayerFactionId}'."));
                continue;
            }
            recruitment.Add(new CampaignRecruitment(definition.Id, offer.StartingStock.Value, definition.RecruitmentCost));
        }
        var recruitmentSites = new List<CampaignRecruitmentSite>();
        var siteIds = new HashSet<string>(StringComparer.Ordinal);
        var sitePointIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var site in document.RecruitmentSites ?? [])
        {
            if (site is null || string.IsNullOrWhiteSpace(site.Id) ||
                string.IsNullOrWhiteSpace(site.PointOfInterestId))
            {
                errors.Add(new ContentError(path, "Each recruitment site must define id and pointOfInterestId."));
                continue;
            }
            if (!siteIds.Add(site.Id))
            {
                errors.Add(new ContentError(path, $"Campaign contains duplicate recruitment site id '{site.Id}'."));
                continue;
            }
            if (!sitePointIds.Add(site.PointOfInterestId))
                errors.Add(new ContentError(path, $"Recruitment point '{site.PointOfInterestId}' is linked by multiple sites."));
            if (map is not null && !map.PointsOfInterest.Any(point => point.Id == site.PointOfInterestId &&
                    point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase) &&
                    point.RecruitmentSiteId == site.Id))
                errors.Add(new ContentError(path, $"Recruitment site '{site.Id}' references missing recruitment point '{site.PointOfInterestId}' linked to that site."));

            if (site.Offers is null || site.Offers.Count == 0)
            {
                errors.Add(new ContentError(path, $"Recruitment site '{site.Id}' must have at least one offer."));
                continue;
            }
            var offers = new List<CampaignRecruitmentOffer>();
            var offeredIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var offer in site.Offers)
            {
                if (offer is null || string.IsNullOrWhiteSpace(offer.UnitId) || offer.StartingStock is null or <= 0)
                {
                    errors.Add(new ContentError(path, $"Recruitment site '{site.Id}' offer requires unitId and positive startingStock."));
                    continue;
                }
                if (!offeredIds.Add(offer.UnitId))
                {
                    errors.Add(new ContentError(path, $"Recruitment site '{site.Id}' contains duplicate offer for '{offer.UnitId}'."));
                    continue;
                }
                var unit = units.Get(offer.UnitId);
                if (unit is null)
                {
                    errors.Add(new ContentError(path, $"Recruitment site '{site.Id}' references unknown unitId '{offer.UnitId}'."));
                    continue;
                }
                if (document.PlayerFactionId is not null && unit.FactionId != document.PlayerFactionId)
                {
                    errors.Add(new ContentError(path, $"Recruitment site '{site.Id}' offer '{offer.UnitId}' does not belong to player faction '{document.PlayerFactionId}'."));
                    continue;
                }
                offers.Add(new CampaignRecruitmentOffer(unit.Id, offer.StartingStock.Value, unit.RecruitmentCost));
            }
            if (offers.Count > 0) recruitmentSites.Add(new CampaignRecruitmentSite(site.Id, site.PointOfInterestId, offers));
        }
        if (recruitment.Count == 0 && recruitmentSites.Count == 0)
            errors.Add(new ContentError(path, "Campaign must offer at least one recruitable unit."));
        if (map is not null)
        {
            foreach (var point in map.PointsOfInterest.Where(point => point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(point.RecruitmentSiteId) ||
                    !recruitmentSites.Any(site => site.Id == point.RecruitmentSiteId && site.PointOfInterestId == point.Id))
                    errors.Add(new ContentError(path, $"Map has unlinked recruitment point '{point.Id}'."));
                if (map.GetTile(point.Coordinate)?.IsPassable != true)
                    errors.Add(new ContentError(path, $"Recruitment point '{point.Id}' must be on a passable tile."));
            }
        }

        var quests = new List<CampaignQuest>();
        var questIds = new HashSet<string>(StringComparer.Ordinal);
        var pointIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var quest in document.Quests ?? [])
        {
            if (quest is null || string.IsNullOrWhiteSpace(quest.Id) || string.IsNullOrWhiteSpace(quest.Title) ||
                string.IsNullOrWhiteSpace(quest.PointOfInterestId) || quest.Choices is null || quest.Choices.Count == 0)
            {
                errors.Add(new ContentError(path, "Each quest must define id, title, pointOfInterestId, and at least one choice."));
                continue;
            }
            if (!questIds.Add(quest.Id))
            {
                errors.Add(new ContentError(path, $"Campaign contains duplicate quest id '{quest.Id}'."));
                continue;
            }
            if (!pointIds.Add(quest.PointOfInterestId))
            {
                errors.Add(new ContentError(path, $"Campaign contains duplicate quest point '{quest.PointOfInterestId}'."));
                continue;
            }
            if (map is not null && !map.PointsOfInterest.Any(point => point.Id == quest.PointOfInterestId &&
                    point.Type.Equals("quest", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(new ContentError(path, $"Quest '{quest.Id}' references missing quest point '{quest.PointOfInterestId}' on map '{map.Id}'."));
                continue;
            }

            var choices = new List<QuestChoice>();
            var choiceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choice in quest.Choices)
            {
                if (choice is null || string.IsNullOrWhiteSpace(choice.Id) || string.IsNullOrWhiteSpace(choice.Description))
                {
                    errors.Add(new ContentError(path, $"Quest '{quest.Id}' has a choice without id or description."));
                    continue;
                }
                if (!choiceIds.Add(choice.Id))
                {
                    errors.Add(new ContentError(path, $"Quest '{quest.Id}' contains duplicate choice id '{choice.Id}'."));
                    continue;
                }
                choices.Add(new QuestChoice(choice.Id, choice.Description, choice.GoldChange, choice.ReputationChange));
            }
            if (choices.Count > 0) quests.Add(new CampaignQuest(quest.PointOfInterestId,
                new Quest(quest.Id, quest.Title, choices)));
        }
        if (document.Quests is null || document.Quests.Count == 0)
            errors.Add(new ContentError(path, "Campaign must define at least one quest."));
        if (map is not null)
        {
            foreach (var point in map.PointsOfInterest.Where(point => point.Type.Equals("quest", StringComparison.OrdinalIgnoreCase)))
                if (!pointIds.Contains(point.Id)) errors.Add(new ContentError(path, $"Map quest point '{point.Id}' has no matching campaign quest."));
        }

        var encounters = new List<CampaignEncounter>();
        var encounterIds = new HashSet<string>(StringComparer.Ordinal);
        var encounterPointIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var encounter in document.Encounters ?? [])
        {
            var initialErrorCount = errors.Count;
            if (encounter is null)
            {
                errors.Add(new ContentError(path, "Each encounter must define id, pointOfInterestId, name, seed, enemyArmy, and rewards."));
                continue;
            }
            Require(encounter.Id, "encounters.id", path, errors);
            Require(encounter.PointOfInterestId, "encounters.pointOfInterestId", path, errors);
            Require(encounter.Name, "encounters.name", path, errors);
            if (encounter.Seed is null)
                errors.Add(new ContentError(path, "Encounter requires an integer seed."));
            if (encounter.GoldReward is null or < 0)
                errors.Add(new ContentError(path, "Encounter goldReward must be zero or a positive integer."));
            if (encounter.ExperienceReward is null or < 0)
                errors.Add(new ContentError(path, "Encounter experienceReward must be zero or a positive integer."));
            if (!string.IsNullOrWhiteSpace(encounter.Id) && !encounterIds.Add(encounter.Id))
                errors.Add(new ContentError(path, $"Campaign contains duplicate encounter id '{encounter.Id}'."));
            if (!string.IsNullOrWhiteSpace(encounter.PointOfInterestId))
            {
                if (!encounterPointIds.Add(encounter.PointOfInterestId))
                    errors.Add(new ContentError(path, $"Point '{encounter.PointOfInterestId}' is linked by multiple encounters."));
                if (map is not null)
                {
                    var point = map.PointsOfInterest.FirstOrDefault(point => point.Id == encounter.PointOfInterestId);
                    if (point is null)
                        errors.Add(new ContentError(path, $"Encounter '{encounter.Id}' references missing point '{encounter.PointOfInterestId}'."));
                    else if (point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase) ||
                             sitePointIds.Contains(point.Id))
                        errors.Add(new ContentError(path, $"Encounter '{encounter.Id}' conflicts with recruitment point '{point.Id}'."));
                }
            }
            var enemyArmy = new List<EncounterTroop>();
            var enemyUnitIds = new HashSet<string>(StringComparer.Ordinal);
            if (encounter.EnemyArmy is null || encounter.EnemyArmy.Count == 0)
                errors.Add(new ContentError(path, $"Encounter '{encounter.Id}' must define a nonempty enemyArmy."));
            else if (encounter.EnemyArmy.Count > 5)
                errors.Add(new ContentError(path, $"Encounter '{encounter.Id}' enemyArmy must contain at most five stacks."));
            foreach (var troop in encounter.EnemyArmy ?? [])
            {
                if (troop is null || string.IsNullOrWhiteSpace(troop.UnitId) || troop.Count is null or <= 0)
                {
                    errors.Add(new ContentError(path, $"Encounter '{encounter.Id}' enemyArmy requires unitId and positive count."));
                    continue;
                }
                if (!enemyUnitIds.Add(troop.UnitId))
                    errors.Add(new ContentError(path, $"Encounter '{encounter.Id}' contains duplicate unit '{troop.UnitId}'."));
                var unit = units.Get(troop.UnitId);
                if (unit is null)
                {
                    errors.Add(new ContentError(path, $"Encounter '{encounter.Id}' references unknown unitId '{troop.UnitId}'."));
                    continue;
                }
                if (unit.FactionId != document.EnemyFactionId)
                    errors.Add(new ContentError(path, $"Encounter unit '{troop.UnitId}' does not belong to enemy faction '{document.EnemyFactionId}'."));
                if ((long)unit.HitPoints * troop.Count.Value > int.MaxValue)
                    errors.Add(new ContentError(path, $"Encounter unit '{troop.UnitId}' total hit points exceed the integer limit."));
                enemyArmy.Add(new EncounterTroop(unit.Id, troop.Count.Value));
            }
            if (errors.Count == initialErrorCount)
                encounters.Add(new CampaignEncounter(encounter.Id!, encounter.PointOfInterestId!, encounter.Name!,
                    encounter.Seed!.Value, enemyArmy, encounter.GoldReward!.Value, encounter.ExperienceReward!.Value));
        }

        if (errors.Count > 0 || map is null || document.Hero is null)
            return new CampaignLoadResult(false, null, errors.AsReadOnly());

        return new CampaignLoadResult(true, new CampaignDefinition(document.Id!, document.Name!, document.Description!,
            document.PlayerFactionId!, document.EnemyFactionId!, mapPath, map, document.Hero.Id!, document.Hero.Name!,
            document.Hero.StartingGold!.Value, recruitment, recruitmentSites, quests, encounters), Array.Empty<ContentError>());
    }

    private static void Require(string? value, string field, string path, ICollection<ContentError> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add(new ContentError(path, $"Required field '{field}' is missing or empty."));
    }

    private static CampaignLoadResult Failed(string path, string message, long? line = null, long? column = null) =>
        new(false, null, new[] { new ContentError(path, message, line, column) });

    private sealed class CampaignDocument
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? PlayerFactionId { get; init; }
        public string? EnemyFactionId { get; init; }
        public string? MapFile { get; init; }
        public HeroDocument? Hero { get; init; }
        public List<RecruitmentDocument?>? Recruitment { get; init; }
        public List<RecruitmentSiteDocument?>? RecruitmentSites { get; init; }
        public List<QuestDocument?>? Quests { get; init; }
        public List<EncounterDocument?>? Encounters { get; init; }
    }
    private sealed class HeroDocument { public string? Id { get; init; } public string? Name { get; init; } public int? StartingGold { get; init; } }
    private sealed class EncounterDocument
    {
        public string? Id { get; init; }
        public string? PointOfInterestId { get; init; }
        public string? Name { get; init; }
        public int? Seed { get; init; }
        public List<EncounterTroopDocument?>? EnemyArmy { get; init; }
        public int? GoldReward { get; init; }
        public int? ExperienceReward { get; init; }
    }
    private sealed class EncounterTroopDocument { public string? UnitId { get; init; } public int? Count { get; init; } }
    private sealed class RecruitmentDocument { public string? UnitId { get; init; } public int? StartingStock { get; init; } }
    private sealed class RecruitmentSiteDocument
    {
        public string? Id { get; init; }
        public string? PointOfInterestId { get; init; }
        public List<RecruitmentDocument?>? Offers { get; init; }
    }
    private sealed class QuestDocument
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? PointOfInterestId { get; init; }
        public List<ChoiceDocument?>? Choices { get; init; }
    }
    private sealed class ChoiceDocument
    {
        public string? Id { get; init; }
        public string? Description { get; init; }
        public int GoldChange { get; init; }
        public int ReputationChange { get; init; }
    }
}
