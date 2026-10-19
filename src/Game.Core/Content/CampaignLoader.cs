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
        if (recruitment.Count == 0) errors.Add(new ContentError(path, "Campaign must offer at least one recruitable unit."));

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

        if (errors.Count > 0 || map is null || document.Hero is null)
            return new CampaignLoadResult(false, null, errors.AsReadOnly());

        return new CampaignLoadResult(true, new CampaignDefinition(document.Id!, document.Name!, document.Description!,
            document.PlayerFactionId!, document.EnemyFactionId!, mapPath, map, document.Hero.Id!, document.Hero.Name!,
            document.Hero.StartingGold!.Value, recruitment, quests), Array.Empty<ContentError>());
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
        public List<QuestDocument?>? Quests { get; init; }
    }
    private sealed class HeroDocument { public string? Id { get; init; } public string? Name { get; init; } public int? StartingGold { get; init; } }
    private sealed class RecruitmentDocument { public string? UnitId { get; init; } public int? StartingStock { get; init; } }
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
