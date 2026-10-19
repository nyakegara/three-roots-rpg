using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Combat;

namespace Game.Core.Content;

/// <summary>Immutable lookup of troop definitions loaded from a standalone JSON catalog.</summary>
public sealed class UnitCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly Dictionary<string, UnitDefinition> _byId;
    private readonly IReadOnlyList<UnitDefinition> _units;

    public IReadOnlyList<UnitDefinition> Units => _units;
    public IReadOnlyList<string> Factions { get; }

    private UnitCatalog(IEnumerable<UnitDefinition> units)
    {
        var definitions = units.ToArray();
        _byId = definitions.ToDictionary(unit => unit.Id, StringComparer.Ordinal);
        _units = Array.AsReadOnly(definitions);
        Factions = Array.AsReadOnly(definitions.Select(unit => unit.FactionId)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public UnitDefinition? Get(string id) => string.IsNullOrWhiteSpace(id) ? null : _byId.GetValueOrDefault(id);

    public static UnitCatalogLoadResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Failed(path ?? string.Empty, "A unit catalog file path is required.");

        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { return Failed(path, $"Could not read unit catalog: {exception.Message}"); }

        UnitDocument? document;
        try { document = JsonSerializer.Deserialize<UnitDocument>(json, JsonOptions); }
        catch (JsonException exception)
        { return Failed(path, $"Invalid JSON: {exception.Message}", exception.LineNumber + 1, exception.BytePositionInLine + 1); }

        if (document?.Units is null || document.Units.Count == 0)
            return Failed(path, "Unit catalog must define at least one unit in 'units'.");

        var errors = new List<ContentError>();
        var units = new List<UnitDefinition>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in document.Units)
        {
            if (unit is null || string.IsNullOrWhiteSpace(unit.Id) || string.IsNullOrWhiteSpace(unit.FactionId) ||
                string.IsNullOrWhiteSpace(unit.Name) || string.IsNullOrWhiteSpace(unit.ArtKey))
            {
                errors.Add(new ContentError(path, "Each unit must define id, factionId, name, and artKey."));
                continue;
            }

            if (!ids.Add(unit.Id))
            {
                errors.Add(new ContentError(path, $"Unit catalog contains duplicate id '{unit.Id}'."));
                continue;
            }

            if (unit.HitPoints is null or <= 0 || unit.Damage is null or <= 0 || unit.Speed is null or <= 0 ||
                unit.Movement is null or <= 0 || unit.Range is null or <= 0 || unit.RecruitmentCost is null or <= 0 ||
                unit.IsRanged is null)
            {
                errors.Add(new ContentError(path, $"Unit '{unit.Id}' has invalid or missing combat and recruitment values."));
                continue;
            }

            if (unit.Ability is null ||
                !Enum.GetNames<UnitAbility>().Contains(unit.Ability, StringComparer.Ordinal) ||
                !Enum.TryParse<UnitAbility>(unit.Ability, ignoreCase: false, out var ability))
            {
                errors.Add(new ContentError(path, $"Unit '{unit.Id}' has unknown ability value '{unit.Ability}'."));
                continue;
            }

            units.Add(new UnitDefinition(unit.Id, unit.FactionId, unit.Name,
                unit.HitPoints.Value, unit.Damage.Value, unit.Speed.Value, unit.Movement.Value,
                unit.Range.Value, unit.IsRanged.Value, unit.RecruitmentCost.Value, unit.ArtKey, ability));
        }

        return errors.Count == 0
            ? new UnitCatalogLoadResult(true, new UnitCatalog(units), Array.Empty<ContentError>())
            : new UnitCatalogLoadResult(false, null, errors.AsReadOnly());
    }

    private static UnitCatalogLoadResult Failed(string path, string message, long? line = null, long? column = null) =>
        new(false, null, new[] { new ContentError(path, message, line, column) });

    private sealed class UnitDocument { public List<UnitEntry?>? Units { get; init; } }
    private sealed class UnitEntry
    {
        public string? Id { get; init; }
        public string? FactionId { get; init; }
        public string? Name { get; init; }
        public int? HitPoints { get; init; }
        public int? Damage { get; init; }
        public int? Speed { get; init; }
        public int? Movement { get; init; }
        public int? Range { get; init; }
        public bool? IsRanged { get; init; }
        public int? RecruitmentCost { get; init; }
        public string? ArtKey { get; init; }
        public string? Ability { get; init; } = nameof(UnitAbility.None);
    }
}
