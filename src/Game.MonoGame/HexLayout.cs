using Game.Core.Combat;
using Game.Core.Geometry;
using Microsoft.Xna.Framework;

namespace Game.MonoGame;

/// <summary>Pointy axial projection. Hit testing uses the exact rendered top polygon.</summary>
internal sealed class HexLayout
{
    public const float Radius = 50;
    public float ElevationStep { get; init; } = 2;
    private const float VerticalCompression = 0.68f;
    private static readonly Vector2 Origin = new(110, 145);

    public Vector2 Center(BattleTile tile) => new(
        Origin.X + MathF.Sqrt(3) * Radius * (tile.Coordinate.Q + tile.Coordinate.R / 2f),
        Origin.Y + 1.5f * Radius * VerticalCompression * tile.Coordinate.R - tile.Height * ElevationStep);

    public Vector2[] Corners(BattleTile tile, float inset = 0)
    {
        var center = Center(tile);
        return Enumerable.Range(0, 6).Select(i =>
        {
            var angle = MathHelper.ToRadians(-90 + 60 * i);
            return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * VerticalCompression) * (Radius - inset);
        }).ToArray();
    }

    public HexCoord? HitTest(Point point, IReadOnlyList<BattleTile> drawOrder)
    {
        // Frontmost polygons win where raised tiles overlap a neighboring top.
        for (var index = drawOrder.Count - 1; index >= 0; index--)
        {
            var corners = Corners(drawOrder[index]);
            var inside = true;
            for (var i = 0; i < corners.Length; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % corners.Length];
                if ((b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X) < 0)
                {
                    inside = false;
                    break;
                }
            }
            if (inside)
                return drawOrder[index].Coordinate;
        }

        return null;
    }
}
