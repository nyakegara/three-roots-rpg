using Game.Core.Combat;
using Game.Core.Combat.Generation;
using Game.Core.Content;
using Game.Core.Geometry;

namespace Game.MonoGame;

/// <summary>A repeatable sandbox encounter; never changes a campaign army.</summary>
public sealed record BattleDemo(GeneratedBattlefield Field, Battle Battle)
{
    public static BattleDemo Create(UnitCatalog catalog, int seed, bool inquisitorPreview = false, bool fullRoster = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var field = new BattlefieldGenerator().Generate(seed);
        var units = new[]
        {
            Stack("flora.thorn-guard", BattleSide.Player, 8, 1, 1),
            Stack("flora.sprout", BattleSide.Player, 5, 2, 2),
            Stack("flora.ember-bloom", BattleSide.Player, 3, 1, 3),
            Stack("flora.pod-slinger", BattleSide.Player, 3, 2, 5),
            Stack("flora.root-warden", BattleSide.Player, 1, 1, 6),
            Stack("human.militia", BattleSide.Enemy, 10, 10, 1),
            Stack(inquisitorPreview ? "human.inquisitor" : "human.banner-knight", BattleSide.Enemy, 3, 10, 3),
            Stack("human.longbow", BattleSide.Enemy, 5, 10, 6)
        };
        var roster = fullRoster ? units.Concat([
            Stack("human.order-hunter", BattleSide.Enemy, 3, 10, 2),
            Stack("human.inquisitor", BattleSide.Enemy, 2, 10, 5)]) : units;
        return new BattleDemo(field, new Battle(field.Map, roster, new Random(seed)));

        BattleUnit Stack(string id, BattleSide side, int count, int col, int row)
        {
            var definition = catalog.Get(id) ?? throw new InvalidOperationException("Missing demo unit: " + id);
            return new BattleUnit(id, side, new HexCoord(col - row / 2, row), count,
                definition.HitPoints, definition.Damage, definition.Speed, definition.Movement,
                definition.Range, definition.IsRanged, ability: definition.Ability, unitTypeId: id);
        }
    }
}
