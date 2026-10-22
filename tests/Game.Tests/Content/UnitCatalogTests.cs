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
        Assert.Equal(UnitAbility.Hunter, units.Single(unit => unit.Id == "human.order-hunter").Ability);
        Assert.Equal(UnitAbility.RadiantModes, units.Single(unit => unit.Id == "human.inquisitor").Ability);
        Assert.Equal(UnitAbility.HealAlly, units.Single(unit => unit.Id == "undead.utburd").Ability);
        Assert.Equal(UnitAbility.SacrificePulse, units.Single(unit => unit.Id == "undead.nachzehrer").Ability);
        Assert.All(units.Where(unit => unit.Id is not "flora.sprout" and not "flora.root-warden" and
            not "human.order-hunter" and not "human.inquisitor" and not "undead.utburd" and not "undead.nachzehrer"),
            unit => Assert.Equal(UnitAbility.None, unit.Ability));
    }

    [Fact]
    public void Load_FloraCatalog_PreservesStableIdsAndDefinesApprovedRoles()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "units", "units.json");

        var result = UnitCatalog.Load(path);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        var units = Assert.IsType<UnitCatalog>(result.Catalog).Units;
        Assert.Equal(17, units.Count);
        Assert.Equal(new[]
        {
            "human.banner-knight", "human.field-medic", "human.inquisitor", "human.longbow", "human.militia",
            "human.order-hunter", "human.spearman",
            "undead.asura-preta", "undead.nachzehrer", "undead.silat", "undead.utburd", "undead.vetala"
        }, units.Where(unit => unit.FactionId != "flora").Select(unit => unit.Id).Order(StringComparer.Ordinal));

        AssertUnit("flora.thorn-guard", "Кабачок-крепыш", 10, 4, 5, 3, 1, false, 8, UnitAbility.None);
        AssertUnit("flora.sprout", "Ипомея-целительница", 8, 3, 7, 4, 6, true, 12, UnitAbility.HealAlly);
        AssertUnit("flora.ember-bloom", "Пламенный цветок", 9, 6, 6, 4, 6, true, 30, UnitAbility.None);
        AssertUnit("flora.pod-slinger", "Орхидея-ассасин", 12, 5, 8, 5, 1, false, 34, UnitAbility.None);
        AssertUnit("flora.root-warden", "Гриб-сновидец", 28, 7, 3, 2, 7, true, 52, UnitAbility.ConvertOnHit);
        Assert.All(units.Where(unit => unit.IsRanged && unit.Ability != UnitAbility.SacrificePulse),
            unit => Assert.InRange(unit.Range, 6, int.MaxValue));

        void AssertUnit(string id, string name, int hp, int damage, int speed, int movement,
            int range, bool ranged, int cost, UnitAbility ability)
        {
            var unit = Assert.Single(units, definition => definition.Id == id);
            Assert.Equal(name, unit.Name);
            Assert.Equal(hp, unit.HitPoints);
            Assert.Equal(damage, unit.Damage);
            Assert.Equal(speed, unit.Speed);
            Assert.Equal(movement, unit.Movement);
            Assert.Equal(range, unit.Range);
            Assert.Equal(ranged, unit.IsRanged);
            Assert.Equal(cost, unit.RecruitmentCost);
            Assert.Equal(ability, unit.Ability);
        }
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
