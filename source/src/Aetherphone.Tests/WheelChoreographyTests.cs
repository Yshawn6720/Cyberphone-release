using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelChoreographyTests
{
    private static readonly float[] StartingAngles =
    {
        0f,
        0.4f,
        1.9f,
        WheelChoreography.Tau * 0.5f,
        WheelChoreography.Tau - 0.001f,
        WheelChoreography.Tau * 3f + 2.2f,
        -WheelChoreography.Tau * 2f - 0.7f,
    };

    [Fact]
    public void EverySegmentLandsUnderThePointerFromEveryStartingAngle()
    {
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            for (var index = 0; index < StartingAngles.Length; index++)
            {
                var from = StartingAngles[index];
                var landed = WheelChoreography.SegmentUnderPointer(from + WheelChoreography.SweepFor(from, segment));
                Assert.Equal(segment, landed);
            }
        }
    }

    [Fact]
    public void TheRestAngleOfASegmentReadsBackAsThatSegment()
    {
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            Assert.Equal(segment, WheelChoreography.SegmentUnderPointer(WheelChoreography.RestAngleOf(segment)));
        }
    }

    [Fact]
    public void ASweepIsAtLeastTheRequestedTurnsAndNeverAWholeTurnMore()
    {
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            for (var index = 0; index < StartingAngles.Length; index++)
            {
                var sweep = WheelChoreography.SweepFor(StartingAngles[index], segment);
                Assert.InRange(sweep,
                    WheelChoreography.SpinTurns * WheelChoreography.Tau,
                    (WheelChoreography.SpinTurns + 1) * WheelChoreography.Tau);
            }
        }
    }

    [Fact]
    public void NormalizeAlwaysAnswersInsideOneTurn()
    {
        var samples = new[] { -12.9f, -0.001f, 0f, 3.1f, WheelChoreography.Tau, 41.7f };
        for (var index = 0; index < samples.Length; index++)
        {
            var wrapped = WheelChoreography.Normalize(samples[index]);
            Assert.InRange(wrapped, 0f, WheelChoreography.Tau);
            Assert.True(wrapped < WheelChoreography.Tau);
        }
    }

    [Fact]
    public void TheSpinEndsExactlyOnTheLandingAndNeverPastIt()
    {
        const float from = 1.1f;
        var sweep = WheelChoreography.SweepFor(from, 37);
        Assert.Equal(from + sweep, WheelChoreography.AngleAt(from, sweep, WheelChoreography.SpinSeconds));
        Assert.Equal(from + sweep, WheelChoreography.AngleAt(from, sweep, WheelChoreography.SpinSeconds + 9f));
    }

    [Fact]
    public void TheDecelerationOnlyEverMovesForwardAndNeverOvershoots()
    {
        const float from = 0.6f;
        var sweep = WheelChoreography.SweepFor(from, 11);
        var target = from + sweep;
        var previous = WheelChoreography.AngleAt(from, sweep, 0f);
        for (var step = 1; step <= 420; step++)
        {
            var elapsed = WheelChoreography.SpinSeconds * step / 420f;
            var angle = WheelChoreography.AngleAt(from, sweep, elapsed);
            Assert.True(angle >= previous);
            Assert.True(angle <= target + 0.0005f);
            previous = angle;
        }

        Assert.Equal(target, previous, 3);
    }

    [Fact]
    public void TheFreeRunBeforeTheCurveMeetsItWithoutAJump()
    {
        const float from = 2.4f;
        var sweep = WheelChoreography.SweepFor(from, 3);
        Assert.Equal(from, WheelChoreography.AngleAt(from, sweep, 0f), 4);
        Assert.True(WheelChoreography.AngleAt(from, sweep, -1.5f) < from);
        Assert.True(WheelChoreography.AngleAt(from, sweep, -1.5f) < WheelChoreography.AngleAt(from, sweep, -0.5f));
        Assert.True(WheelChoreography.AngleAt(from, sweep, -0.5f) < from);
    }

    [Fact]
    public void ProgressClampsToTheSpinWindow()
    {
        Assert.Equal(0f, WheelChoreography.Progress(-3f));
        Assert.Equal(0f, WheelChoreography.Progress(0f));
        Assert.Equal(0.5f, WheelChoreography.Progress(WheelChoreography.SpinSeconds * 0.5f), 4);
        Assert.Equal(1f, WheelChoreography.Progress(WheelChoreography.SpinSeconds));
        Assert.Equal(1f, WheelChoreography.Progress(WheelChoreography.SpinSeconds + 10f));
    }

    [Fact]
    public void FiftySpansCoverExactlyOneTurn()
    {
        Assert.Equal(WheelChoreography.Tau, WheelChoreography.SegmentSpan * WheelRules.SegmentCount, 4);
    }
}
