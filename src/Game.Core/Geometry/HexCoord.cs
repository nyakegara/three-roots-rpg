namespace Game.Core.Geometry;

/// <summary>
/// Identifies a hexagon on an axial grid.
/// </summary>
/// <param name="Q">The first axial coordinate.</param>
/// <param name="R">The second axial coordinate.</param>
public readonly record struct HexCoord(int Q, int R)
{
    /// <summary>
    /// Returns the six hexagons directly next to this one.
    /// </summary>
    /// <exception cref="OverflowException">
    /// A neighboring coordinate would fall outside the range of <see cref="int"/>.
    /// </exception>
    public IReadOnlyList<HexCoord> GetNeighbors()
    {
        return
        [
            new HexCoord(checked(Q + 1), R),
            new HexCoord(checked(Q + 1), checked(R - 1)),
            new HexCoord(Q, checked(R - 1)),
            new HexCoord(checked(Q - 1), R),
            new HexCoord(checked(Q - 1), checked(R + 1)),
            new HexCoord(Q, checked(R + 1))
        ];
    }

    /// <summary>
    /// Calculates the shortest number of hexagon steps between two coordinates.
    /// </summary>
    /// <exception cref="OverflowException">
    /// The distance is larger than <see cref="int.MaxValue"/>.
    /// </exception>
    public int DistanceTo(HexCoord other)
    {
        var deltaQ = (long)Q - other.Q;
        var deltaR = (long)R - other.R;
        var distance = Math.Max(Math.Abs(deltaQ), Math.Max(Math.Abs(deltaR), Math.Abs(deltaQ + deltaR)));

        return checked((int)distance);
    }
}
