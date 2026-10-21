namespace Aetherphone.Core.Animation;

internal static class TransitionTiming
{
    public const float PresentSmoothTime = 0.15f;
    public const float DismissSmoothTime = 0.13f;
    public const float ZoomPresentSmoothTime = 0.145f;
    public const float ZoomDismissSmoothTime = 0.135f;
    public const float InteractiveProgress = 0.8f;
    public const float ScrubReturnKickFraction = 0.35f;
    public const float ScrubKickCapFactor = 3f;
    public const float HomeZoomDepth = 0.32f;
    public const float HomeRecedeDim = 0.45f;
    public const float PushSmoothTime = 0.13f;
    public const float ShellDimMax = 0.45f;
    public const float UnderParallax = 0.26f;
    public const float UnderDimMax = 0.16f;
    public const float MaxFrameSeconds = 0.1f;
    public const float MotionFrameSeconds = 1f / 30f;
    public const float LaunchKickOmegaFraction = 0.5f;
    public const float BubbleSeconds = 0.34f;
    public const float RestPositionEpsilon = 0.0015f;
    public const float RestVelocityEpsilon = 0.02f;
    public const float MotionSettleEpsilon = 0.006f;

    public static float LaunchVelocity(float smoothTime) =>
        LaunchKickOmegaFraction * 2f / MathF.Max(0.0001f, smoothTime);
}
