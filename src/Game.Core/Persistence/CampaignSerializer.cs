using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Persistence;

/// <summary>Reads and atomically writes versioned campaign progress as JSON.</summary>
public static class CampaignSerializer
{
    private static readonly string[] RequiredFields =
    [
        "schemaVersion", "campaignId", "heroId", "heroName", "mapId", "heroPosition", "gold",
        "reputation", "experience", "level", "skillPoints", "army", "completedQuestIds",
        "selectedChoices", "skillIds", "artifactIds", "recruitmentStocks"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static SaveResult Save(string path, CampaignSave save)
    {
        if (string.IsNullOrWhiteSpace(path)) return new SaveResult(false, "A save file path is required.");
        if (save is null) return new SaveResult(false, "Campaign progress is required.");
        var validationError = Validate(save);
        if (validationError is not null) return new SaveResult(false, validationError);

        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return new SaveResult(false, $"Invalid save file path: {exception.Message}"); }
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(save, JsonOptions));
            File.Move(temporaryPath, fullPath, overwrite: true);
            return new SaveResult(true, "Campaign progress saved.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new SaveResult(false, $"Could not save campaign progress: {exception.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static LoadResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Failed("A save file path is required.");
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return Failed($"Invalid save file path: {exception.Message}"); }
        if (!File.Exists(fullPath)) return Failed($"Save file '{fullPath}' was not found.");

        CampaignSave? save;
        try
        {
            var json = File.ReadAllText(fullPath);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Failed("Save file must contain a campaign progress object.");
            var availableFields = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
            var missingField = RequiredFields.FirstOrDefault(field =>
                !availableFields.Contains(field, StringComparer.OrdinalIgnoreCase));
            if (missingField is not null) return Failed($"Save file is missing required field '{missingField}'.");
            save = JsonSerializer.Deserialize<CampaignSave>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            return Failed($"Save file contains invalid JSON: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failed($"Could not read save file: {exception.Message}");
        }

        if (save is null) return Failed("Save file must contain a campaign progress object.");
        if (save.SchemaVersion != CampaignSave.CurrentSchemaVersion)
            return Failed($"Save schema version {save.SchemaVersion} is not supported (expected {CampaignSave.CurrentSchemaVersion}).");
        var validationError = Validate(save);
        return validationError is null
            ? new LoadResult(true, save, "Campaign progress loaded.")
            : Failed($"Save file is invalid: {validationError}");
    }

    private static string? Validate(CampaignSave save)
    {
        if (save.SchemaVersion != CampaignSave.CurrentSchemaVersion)
            return $"Unsupported schema version {save.SchemaVersion}.";
        if (string.IsNullOrWhiteSpace(save.CampaignId) || string.IsNullOrWhiteSpace(save.HeroId) ||
            string.IsNullOrWhiteSpace(save.HeroName) || string.IsNullOrWhiteSpace(save.MapId))
            return "Campaign, hero, and map ids and the hero name are required.";
        if (save.Gold < 0 || save.Reputation is < -3 or > 3 || save.Experience < 0 ||
            save.Level < 1 || save.SkillPoints < 0)
            return "Hero resources, level, or experience are outside their valid range.";
        if (save.Army is null || save.CompletedQuestIds is null || save.SelectedChoices is null ||
            save.SkillIds is null || save.ArtifactIds is null || save.RecruitmentStocks is null)
            return "Save collections must not be null.";
        if (save.Army.Count > 5 || save.Army.Any(stack => stack is null || string.IsNullOrWhiteSpace(stack.UnitId) || stack.Count <= 0) ||
            save.Army.Select(stack => stack.UnitId).Distinct(StringComparer.Ordinal).Count() != save.Army.Count)
            return "Army must contain up to five unique unit ids with positive counts.";
        if (!HasUniqueNonEmptyIds(save.CompletedQuestIds) ||
            save.SelectedChoices.Any(choice => choice is null || string.IsNullOrWhiteSpace(choice.QuestId) || string.IsNullOrWhiteSpace(choice.ChoiceId)) ||
            save.SelectedChoices.Select(choice => choice.QuestId).Distinct(StringComparer.Ordinal).Count() != save.SelectedChoices.Count ||
            save.SelectedChoices.Count != save.CompletedQuestIds.Count ||
            save.SelectedChoices.Any(choice => !save.CompletedQuestIds.Contains(choice.QuestId, StringComparer.Ordinal)))
            return "Completed quests and selected choices must have unique, matching ids.";
        if (!HasUniqueNonEmptyIds(save.SkillIds) || !HasUniqueNonEmptyIds(save.ArtifactIds))
            return "Skill and artifact ids must be non-empty and unique.";
        if (save.RecruitmentStocks.Any(stock => stock is null ||
                string.IsNullOrWhiteSpace(stock.SiteId) || string.IsNullOrWhiteSpace(stock.UnitId) || stock.RemainingStock < 0) ||
            save.RecruitmentStocks.Select(stock => (stock.SiteId, stock.UnitId)).Distinct().Count() != save.RecruitmentStocks.Count)
            return "Recruitment stock must contain unique site and unit ids with non-negative remaining stock.";
        if (save.CompletedEncounterIds is null || !HasUniqueNonEmptyIds(save.CompletedEncounterIds))
            return "Completed encounter ids must be non-empty and unique.";
        if (save.IsDefeated && save.Army.Count != 0)
            return "A defeated campaign cannot have a surviving army.";
        return null;
    }

    private static bool HasUniqueNonEmptyIds(IReadOnlyList<string> ids) =>
        !ids.Any(string.IsNullOrWhiteSpace) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Count;

    private static LoadResult Failed(string message) => new(false, null, message);
}
