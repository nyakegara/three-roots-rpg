using Game.Core.Geometry;

namespace Game.Core.Combat.Generation;

/// <summary>Authored pass with a narrow breach and two elevated archer positions.</summary>
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
            var isWall = column == 6 && row is not (3 or 4);
            if (isWall)
            {
                props.Add(new BattlefieldProp(coordinate, BattlefieldPropKind.Rock));
            }

            var height = column >= 8 && row is 1 or 2 or 5 or 6 ? 2 :
                column == 7 && row is 1 or 2 or 5 or 6 ? 1 : 0;
            tiles.Add(new BattleTile(coordinate, height, !isWall));
        }

        return new GeneratedBattlefield(seed, new BattleMap(tiles), props,
            At(2, 4), At(9, 4));
    }

    private static HexCoord At(int column, int row) => new(column - row / 2, row);
}
