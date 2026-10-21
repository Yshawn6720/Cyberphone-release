using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Theme;

internal static class CaseSwap
{
    private const float SmoothTime = 0.32f;
    private const float RestEpsilon = 0.006f;

    private static PhoneCase? outgoing;
    private static Spring progress;
    private static int lastSteppedFrame = -1;

    public static bool Active => outgoing is not null;

    public static PhoneCase Outgoing => outgoing!;

    public static float Progress => progress.Value;

    public static void Begin(PhoneCase previous)
    {
        outgoing = previous;
        progress.SnapTo(0f);
    }

    public static void Step(int frame, float deltaSeconds)
    {
        if (outgoing is null || frame == lastSteppedFrame)
        {
            return;
        }

        lastSteppedFrame = frame;
        progress.Step(1f, SmoothTime, deltaSeconds);
        if (progress.IsResting(1f, RestEpsilon, RestEpsilon))
        {
            progress.SnapTo(1f);
            outgoing = null;
        }
    }
}
