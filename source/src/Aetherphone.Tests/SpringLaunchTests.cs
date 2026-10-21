using Aetherphone.Core.Animation;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SpringLaunchTests
{
    private const float SixtyHertz = 1f / 60f;
    private const float ThirtyHertz = 1f / 30f;
    private const int MaxFrames = 240;

    public static TheoryData<float, float> SmoothTimesAndFrameRates()
    {
        var data = new TheoryData<float, float>();
        float[] smoothTimes =
        {
            TransitionTiming.ZoomPresentSmoothTime, TransitionTiming.ZoomDismissSmoothTime,
            TransitionTiming.PresentSmoothTime, TransitionTiming.DismissSmoothTime, TransitionTiming.PushSmoothTime,
        };
        float[] frameSeconds = { SixtyHertz, ThirtyHertz, TransitionTiming.MotionFrameSeconds };
        for (var smoothIndex = 0; smoothIndex < smoothTimes.Length; smoothIndex++)
        {
            for (var frameIndex = 0; frameIndex < frameSeconds.Length; frameIndex++)
            {
                data.Add(smoothTimes[smoothIndex], frameSeconds[frameIndex]);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SmoothTimesAndFrameRates))]
    public void LaunchedPresentNeverOvershootsAndOnlyMovesForward(float smoothTime, float frameSeconds)
    {
        var spring = new Spring();
        spring.Launch(0f, TransitionTiming.LaunchVelocity(smoothTime));
        var previous = 0f;
        for (var frame = 0; frame < MaxFrames; frame++)
        {
            var value = spring.Step(1f, smoothTime, frameSeconds);
            Assert.True(value >= previous - 1e-6f, $"frame {frame} moved backwards: {previous} -> {value}");
            Assert.True(value <= 1f, $"frame {frame} overshot: {value}");
            previous = value;
        }

        Assert.True(spring.IsResting(1f, TransitionTiming.MotionSettleEpsilon, 1f));
    }

    [Theory]
    [MemberData(nameof(SmoothTimesAndFrameRates))]
    public void LaunchedDismissMirrorsPresent(float smoothTime, float frameSeconds)
    {
        var spring = new Spring();
        spring.Launch(1f, -TransitionTiming.LaunchVelocity(smoothTime));
        var previous = 1f;
        for (var frame = 0; frame < MaxFrames; frame++)
        {
            var value = spring.Step(0f, smoothTime, frameSeconds);
            Assert.True(value <= previous + 1e-6f, $"frame {frame} moved backwards: {previous} -> {value}");
            Assert.True(value >= 0f, $"frame {frame} overshot: {value}");
            previous = value;
        }

        Assert.True(spring.IsResting(0f, TransitionTiming.MotionSettleEpsilon, 1f));
    }

    [Fact]
    public void KickMovesTheCardOnTheVeryFirstFrame()
    {
        var spring = new Spring();
        spring.Launch(0f, TransitionTiming.LaunchVelocity(TransitionTiming.ZoomPresentSmoothTime));
        var first = spring.Step(1f, TransitionTiming.ZoomPresentSmoothTime, SixtyHertz);
        Assert.InRange(first, 0.04f, 0.15f);
    }

    [Fact]
    public void ZoomPresentReadsAsSnappyAndSettlesUnderHalfASecond()
    {
        var spring = new Spring();
        spring.Launch(0f, TransitionTiming.LaunchVelocity(TransitionTiming.ZoomPresentSmoothTime));
        var elapsed = 0f;
        var halfAt = float.MaxValue;
        var ninetyAt = float.MaxValue;
        var settledAt = float.MaxValue;
        for (var frame = 0; frame < MaxFrames && settledAt == float.MaxValue; frame++)
        {
            elapsed += SixtyHertz;
            var value = spring.Step(1f, TransitionTiming.ZoomPresentSmoothTime, SixtyHertz);
            halfAt = value >= 0.5f ? MathF.Min(halfAt, elapsed) : halfAt;
            ninetyAt = value >= 0.9f ? MathF.Min(ninetyAt, elapsed) : ninetyAt;
            if (1f - value <= TransitionTiming.MotionSettleEpsilon)
            {
                settledAt = elapsed;
            }
        }

        Assert.True(halfAt <= 0.12f, $"reached 50% at {halfAt:F3}s");
        Assert.True(ninetyAt <= 0.28f, $"reached 90% at {ninetyAt:F3}s");
        Assert.True(settledAt <= 0.5f, $"settled at {settledAt:F3}s");
    }

    [Fact]
    public void HitchClampTurnsAFrameDropIntoSlownessNotASkip()
    {
        var hitched = new Spring();
        hitched.Launch(0f, TransitionTiming.LaunchVelocity(TransitionTiming.ZoomPresentSmoothTime));
        var afterHitch = hitched.Step(1f, TransitionTiming.ZoomPresentSmoothTime,
            MathF.Min(TransitionTiming.MaxFrameSeconds, TransitionTiming.MotionFrameSeconds));
        Assert.True(afterHitch < 0.3f, $"a single clamped frame jumped to {afterHitch:F3}");
    }
}
