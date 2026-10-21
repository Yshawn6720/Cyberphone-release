using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Shell;

internal sealed class OrientationTurn
{
    public const float TurnSeconds = 0.36f;
    private const float FadeSpan = 0.28f;
    private const float QuarterTurn = MathF.PI * 0.5f;
    private const float MinimumGrowth = 0.01f;

    private float progress;

    public bool Turning => progress > 0f && progress < 1f;

    public bool ShowsLandscape => progress >= 0.5f;

    public float Angle
    {
        get
        {
            var eased = Easing.SmootherStep(progress);
            return ShowsLandscape ? QuarterTurn * (1f - eased) : -QuarterTurn * eased;
        }
    }

    public float ContentAlpha
    {
        get
        {
            var edge = MathF.Min(progress, 1f - progress);
            return edge >= FadeSpan ? 0f : Easing.SmootherStep(1f - edge / FadeSpan);
        }
    }

    public float TravelTo(bool landscape)
    {
        var eased = Easing.SmootherStep(progress);
        return landscape ? eased : 1f - eased;
    }

    public float ScaleFor(float growth)
    {
        var safe = MathF.Max(growth, MinimumGrowth);
        var eased = Easing.SmootherStep(progress);
        return ShowsLandscape ? Easing.Lerp(1f / safe, 1f, eased) : Easing.Lerp(1f, safe, eased);
    }

    public void Advance(float deltaSeconds, bool wantsLandscape, bool animates)
    {
        var target = wantsLandscape ? 1f : 0f;
        if (!animates)
        {
            progress = target;
            return;
        }

        var step = deltaSeconds / TurnSeconds;
        progress = target > progress
            ? MathF.Min(target, progress + step)
            : MathF.Max(target, progress - step);
    }
}
