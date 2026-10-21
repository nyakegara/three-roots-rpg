namespace Game.MonoGame.Rendering;

/// <summary>One clock for the action pose, bow release and projectile launch.</summary>
internal static class AttackAnimationTiming
{
    public const float Duration = 0.8f;
    public const float FrameSeconds = 0.1f;
    public const int ReleaseFrame = 2;
    public const float ReleaseSeconds = ReleaseFrame * FrameSeconds;

    public static int FrameAt(float remainingSeconds) =>
        Math.Clamp((int)((Duration - remainingSeconds + 0.00001f) / FrameSeconds), 0, 7);
}
