namespace Aetherphone.Core.Animation;

internal static class FrameClock
{
    private const float BlendWeight = 0.3f;
    private const float CadenceBreakRatio = 0.5f;

    private static float smoothed;
    private static int stampedFrame = -1;

    public static float Delta { get; private set; }

    public static void Advance(int frame, float rawDelta)
    {
        if (frame == stampedFrame)
        {
            return;
        }

        stampedFrame = frame;
        Delta = Smooth(Math.Clamp(rawDelta, 0f, TransitionTiming.MaxFrameSeconds));
    }

    public static float Smooth(float sample)
    {
        if (smoothed <= 0f || MathF.Abs(sample - smoothed) > smoothed * CadenceBreakRatio)
        {
            smoothed = sample;
        }
        else
        {
            smoothed += (sample - smoothed) * BlendWeight;
        }

        return smoothed;
    }

    public static void Reset()
    {
        smoothed = 0f;
        stampedFrame = -1;
        Delta = 0f;
    }
}
