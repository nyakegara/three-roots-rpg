using Game.Core.Content;
using Game.Core.Combat;
using Xunit;

namespace Game.Tests.Content;

public sealed class UnitCatalogTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    [Fact]
    public void Load_FloraCatalog_DeclaresExpectedAbilitiesAndDefaultsOthersToNone()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "units", "units.json");

        var result = UnitCatalog.Load(path);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        var units = Assert.IsType<UnitCatalog>(result.Catalog).Units;
        Assert.Equal(UnitAbility.HealAlly, units.Single(unit => unit.Id == "flora.sprout").Ability);
        Assert.Equal(UnitAbility.ConvertOnHit, units.Single(unit => unit.Id == "flora.root-warden").Ability);
        Assert.All(units.Where(unit => unit.Id is not "flora.sprout" and not "flora.root-warden"),
            unit => Assert.Equal(UnitAbility.None, unit.Ability));
    }

    [Fact]
    public void Load_UnknownAbility_ReturnsClearDiagnostic()
    {
        var path = WriteUnits("""
        {"units":[{"id":"flora.scout","factionId":"flora","name":"Разведчик","hitPoints":8,"damage":3,"speed":7,"movement":5,"range":1,"isRanged":false,"recruitmentCost":12,"artKey":"unit.flora.scout","ability":"Teleport"}]}
        """);

        var result = UnitCatalog.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Errors, error => error.Message.Contains("ability", StringComparison.OrdinalIgnoreCase) &&
                                                error.Message.Contains("Teleport", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_ValidDefinitions_ReturnsLookupByStableId()
    {
        var path = WriteUnits("""
        {"units":[{"id":"flora.scout","factionId":"flora","name":"Побег-разведчик","hitPoints":8,"damage":3,"speed":7,"movement":5,"range":1,"isRanged":false,"recruitmentCost":12,"artKey":"unit.flora.scout"}]}
        """);

        var result = UnitCatalog.Load(path);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Catalog);
        var unit = Assert.IsType<UnitDefinition>(result.Catalog.Get("flora.scout"));
        Assert.Equal("flora", unit.FactionId);
        Assert.Equal(8, unit.HitPoints);
        Assert.Equal(12, unit.RecruitmentCost);
    }

    [Fact]
    public void Load_DuplicateIds_ReturnsDiagnosticInsteadOfReplacingDefinition()
    {
        var path = WriteUnits("""
        {"units":[
          {"id":"flora.scout","factionId":"flora","name":"Первый","hitPoints":8,"damage":3,"speed":7,"movement":5,"range":1,"isRanged":false,"recruitmentCost":12,"artKey":"unit.flora.scout"},
          {"id":"flora.scout","factionId":"flora","name":"Второй","hitPoints":8,"damage":3,"speed":7,"movement":5,"range":1,"isRanged":false,"recruitmentCost":12,"artKey":"unit.flora.scout"}
        ]}
        """);

        var result = UnitCatalog.Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Errors, error => error.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles) File.Delete(path);
    }

    private string WriteUnits(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"units-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _temporaryFiles.Add(path);
        return path;
    }
}
