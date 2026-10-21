using Aetherphone.Core.Animation;
using Xunit;

namespace Aetherphone.Tests;

[Collection("FrameClock")]
public sealed class FrameClockTests
{
    private const float SixtyHertz = 1f / 60f;

    [Fact]
    public void JitterAroundASteadyCadenceIsSmoothedTowardTheCadence()
    {
        FrameClock.Reset();
        FrameClock.Smooth(SixtyHertz);
        float[] jitter = { 0.014f, 0.019f, 0.015f, 0.018f, 0.014f, 0.019f, 0.016f, 0.017f };
        var maxDeviation = 0f;
        for (var index = 0; index < jitter.Length; index++)
        {
            var smoothed = FrameClock.Smooth(jitter[index]);
            maxDeviation = MathF.Max(maxDeviation, MathF.Abs(smoothed - SixtyHertz));
        }

        Assert.True(maxDeviation < 0.0015f, $"smoothed delta strayed {maxDeviation} from the cadence");
    }

    [Fact]
    public void ACadenceBreakSnapsInsteadOfLagging()
    {
        FrameClock.Reset();
        FrameClock.Smooth(SixtyHertz);
        var thirty = FrameClock.Smooth(1f / 30f);
        Assert.Equal(1f / 30f, thirty, 5);
        var hitch = FrameClock.Smooth(TransitionTiming.MaxFrameSeconds);
        Assert.Equal(TransitionTiming.MaxFrameSeconds, hitch, 5);
    }

    [Fact]
    public void AdvanceStampsOncePerFrameAndClampsTheSample()
    {
        FrameClock.Reset();
        FrameClock.Advance(1, 0.5f);
        Assert.Equal(TransitionTiming.MaxFrameSeconds, FrameClock.Delta, 5);
        FrameClock.Advance(1, SixtyHertz);
        Assert.Equal(TransitionTiming.MaxFrameSeconds, FrameClock.Delta, 5);
        FrameClock.Advance(2, SixtyHertz);
        Assert.Equal(SixtyHertz, FrameClock.Delta, 5);
    }
}
