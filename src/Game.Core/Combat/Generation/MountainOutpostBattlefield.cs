using Game.Core.Geometry;

namespace Game.Core.Combat.Generation;

/// <summary>Authored pass with a continuous eastern plateau and a broad southern ascent.</summary>
public static class MountainOutpostBattlefield
{
    public static GeneratedBattlefield Create(int seed)
    {
        const int columns = 12;
        const int rows = 8;
        var tiles = new List<BattleTile>();
        var props = new List<BattlefieldProp>();
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            var coordinate = At(column, row);
            var isWall = column == 6 && row < 5;
            if (isWall)
            {
                props.Add(new BattlefieldProp(coordinate, BattlefieldPropKind.Rock));
            }

            var height = column >= 7 ? 2 : column == 6 ? 1 : 0;
            tiles.Add(new BattleTile(coordinate, height, !isWall, blocksLineOfSight: false));
        }

        return new GeneratedBattlefield(seed, new BattleMap(tiles), props,
            At(3, 3), At(9, 2), isMountainOutpost: true);
    }

    private static HexCoord At(int column, int row) => new(column - row / 2, row);
}
