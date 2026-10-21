using System.Numerics;

namespace Game.Core.Geometry;

/// <summary>
/// Aligns the invisible axial travel grid with the landmarks painted on Flora's island.
/// The anchors are positions in the 1862×845 master art, before and after alignment.
/// This affects only where tiles are drawn and picked, never their travel rules.
/// </summary>
internal static class FloraIslandArtAlignment
{
    private const float ArtWidth = 1862;
    private const float ArtHeight = 845;
    private const double Softening = 900;
    private const double UnchangedWeight = 1.0 / (250.0 * 250.0 * 250.0 * 250.0);

    private readonly record struct Anchor(float FromX, float FromY, float ToX, float ToY);

    private static readonly Anchor[] Landmarks =
    [
        new(1446, 318, 1530, 230), // starting forest
        new(1314, 457, 1320, 545), // pasture
        new(1139, 338, 1117, 408), // northern trail entrance
        new(1183, 278, 1117, 275), // burned forest
        new(701, 293, 824, 492),  // pass through the mountain ridge
        new(570, 437, 671, 706),  // mushroom clearing
        new(350, 298, 235, 455),  // ship landing
        new(548, 268, 442, 623),  // western artifact
        new(372, 388, 561, 727),  // mushroom artifact
        new(1446, 517, 1390, 650) // pasture patrol
    ];

    public static Vector2 Warp(Vector2 position, Vector2 worldSize)
    {
        var x = position.X / worldSize.X * ArtWidth;
        var y = position.Y / worldSize.Y * ArtHeight;
        var totalWeight = UnchangedWeight;
        double displacementX = 0;
        double displacementY = 0;

        foreach (var anchor in Landmarks)
        {
            var dx = x - anchor.FromX;
            var dy = y - anchor.FromY;
            var distanceSquared = dx * dx + dy * dy;
            var weight = 1.0 / Math.Pow(distanceSquared + Softening, 2);
            totalWeight += weight;
            displacementX += weight * (anchor.ToX - anchor.FromX);
            displacementY += weight * (anchor.ToY - anchor.FromY);
        }

        return new Vector2((float)((x + displacementX / totalWeight) / ArtWidth * worldSize.X),
            (float)((y + displacementY / totalWeight) / ArtHeight * worldSize.Y));
    }
}
