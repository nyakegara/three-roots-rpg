using Microsoft.Xna.Framework;

namespace Game.MonoGame;

/// <summary>Small ambient-palette helpers for the battle preview.</summary>
public static class BattleLighting
{
    public static Color ExplorationTint(float nightBlend) =>
        Color.Lerp(Color.White, new Color(125, 145, 195), Math.Clamp(nightBlend, 0f, 1f));

    public static readonly Color DayTint = Color.White;
    public static readonly Color NightTint = new(145, 155, 225);
    public const float FadeDurationSeconds = 1.2f;

    public static float AdvanceFade(float current, float target, float elapsedSeconds,
        float durationSeconds = FadeDurationSeconds)
    {
        current = float.IsFinite(current) ? Math.Clamp(current, 0f, 1f) : 0f;
        target = float.IsFinite(target) ? Math.Clamp(target, 0f, 1f) : 0f;
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
            return current;
        if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
            return target;

        var amount = elapsedSeconds / durationSeconds;
        return current < target
            ? MathF.Min(target, current + amount)
            : MathF.Max(target, current - amount);
    }

    public static Color AmbientTint(float nightBlend) =>
        Color.Lerp(DayTint, NightTint, float.IsFinite(nightBlend) ? Math.Clamp(nightBlend, 0f, 1f) : 0f);

    public static Color ForegroundTint(float nightBlend) =>
        Color.Lerp(DayTint, new Color(190, 200, 240),
            float.IsFinite(nightBlend) ? Math.Clamp(nightBlend, 0f, 1f) : 0f);

    public static Color TintPreservingAlpha(Color source, Color tint) =>
        new(Multiply(source.R, tint.R), Multiply(source.G, tint.G),
            Multiply(source.B, tint.B), source.A);

    private static byte Multiply(byte first, byte second) =>
        (byte)((first * second + 127) / 255);
}
