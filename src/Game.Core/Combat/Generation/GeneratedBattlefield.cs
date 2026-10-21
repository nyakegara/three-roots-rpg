using Game.Core.Geometry;

namespace Game.Core.Combat.Generation;

/// <summary>A generated field owns its terrain and a read-only snapshot of its scenery.</summary>
public sealed class GeneratedBattlefield
{
    public int Seed { get; }
    public BattleMap Map { get; }
    public IReadOnlyList<BattlefieldProp> Props { get; }
    public HexCoord PlayerStart { get; }
    public HexCoord EnemyStart { get; }

    internal GeneratedBattlefield(int seed, BattleMap map, IEnumerable<BattlefieldProp> props,
        HexCoord playerStart, HexCoord enemyStart)
    {
        Seed = seed;
        Map = map;
        Props = Array.AsReadOnly(props.ToArray());
        PlayerStart = playerStart;
        EnemyStart = enemyStart;
    }
}
