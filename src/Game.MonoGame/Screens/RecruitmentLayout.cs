using Microsoft.Xna.Framework;

namespace Game.MonoGame.Screens;

/// <summary>Pure UI bounds and hit testing shared by rendering and mouse input.</summary>
public static class RecruitmentLayout
{
    public static Rectangle QuantityMinusBounds { get; } = new(800, 348, 54, 38);
    public static Rectangle QuantityValueBounds { get; } = new(862, 348, 54, 38);
    public static Rectangle QuantityPlusBounds { get; } = new(924, 348, 54, 38);

    public static int QuantityDeltaAt(int x, int y)
    {
        if (QuantityMinusBounds.Contains(x, y)) return -1;
        if (QuantityPlusBounds.Contains(x, y)) return 1;
        return 0;
    }
}
