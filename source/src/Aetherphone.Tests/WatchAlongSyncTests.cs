using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WatchAlongSyncTests
{
    private const float Tick = 1f / 60f;

    [Fact]
    public void ServerClockKeepsTheLeastDelayedSample()
    {
        var clock = new ServerClock();
        Assert.False(clock.Anchored);
        Assert.Equal(1_000L, clock.ServerNowUnixMs(1_000L));

        clock.Absorb(serverStampUnixMs: 10_000L, localReceivedUnixMs: 10_250L);
        clock.Absorb(serverStampUnixMs: 18_000L, localReceivedUnixMs: 18_040L);
        clock.Absorb(serverStampUnixMs: 26_000L, localReceivedUnixMs: 26_400L);

        Assert.True(clock.Anchored);
        Assert.Equal(-40L, clock.SkewMilliseconds);
        Assert.Equal(30_000L - 40L, clock.ServerNowUnixMs(30_000L));
    }

    [Fact]
    public void ServerClockForgetsSamplesOutsideTheWindow()
    {
        var clock = new ServerClock();
        clock.Absorb(serverStampUnixMs: 5_000L, localReceivedUnixMs: 4_000L);
        for (var index = 0; index < ServerClock.SampleWindow; index++)
        {
            var stamp = 10_000L + (index * 8_000L);
            clock.Absorb(stamp, stamp + 100L);
        }

        Assert.Equal(-100L, clock.SkewMilliseconds);
    }

    [Fact]
    public void ServerClockIgnoresMissingStampsAndResets()
    {
        var clock = new ServerClock();
        clock.Absorb(serverStampUnixMs: 0L, localReceivedUnixMs: 5_000L);
        Assert.False(clock.Anchored);

        clock.Absorb(serverStampUnixMs: 7_000L, localReceivedUnixMs: 5_000L);
        Assert.Equal(2_000L, clock.SkewMilliseconds);

        clock.Reset();
        Assert.False(clock.Anchored);
        Assert.Equal(0L, clock.SkewMilliseconds);
    }

    [Fact]
    public void ControllerHoldsInsideTheDeadZone()
    {
        var controller = new PlaybackSyncController();

        var decision = controller.Step(localPosition: 100.2, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        Assert.Equal(SyncDecision.Hold, decision);
        Assert.Equal(1d, controller.AppliedSpeed);
    }

    [Fact]
    public void ControllerSlowsDownWhenAheadAndSpeedsUpWhenBehind()
    {
        var controller = new PlaybackSyncController();

        var ahead = controller.Step(localPosition: 101.0, targetPosition: 100.0, durationSeconds: 600, false, Tick);
        Assert.True(ahead.SpeedChanged);
        Assert.False(ahead.Seek);
        Assert.InRange(ahead.Speed, 1d - PlaybackSyncController.MaxSpeedDeviation, 1d - PlaybackSyncController.SpeedStep);

        var behind = controller.Step(localPosition: 99.0, targetPosition: 100.0, durationSeconds: 600, false, Tick);
        Assert.True(behind.SpeedChanged);
        Assert.InRange(behind.Speed, 1d + PlaybackSyncController.SpeedStep, 1d + PlaybackSyncController.MaxSpeedDeviation);
    }

    [Fact]
    public void ControllerCapsTheNudgeAtTheMaximumDeviation()
    {
        var controller = new PlaybackSyncController();

        var decision = controller.Step(localPosition: 102.5, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        Assert.True(decision.SpeedChanged);
        Assert.Equal(1d - PlaybackSyncController.MaxSpeedDeviation, decision.Speed, precision: 6);
    }

    [Fact]
    public void ControllerDoesNotChurnOnTinyDriftChanges()
    {
        var controller = new PlaybackSyncController();
        var first = controller.Step(localPosition: 101.0, targetPosition: 100.0, durationSeconds: 600, false, Tick);
        Assert.True(first.SpeedChanged);

        var second = controller.Step(localPosition: 101.001, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        Assert.False(second.SpeedChanged);
        Assert.False(second.Seek);
    }

    [Fact]
    public void ControllerSeeksPastTheHardToleranceAndRestoresNormalSpeed()
    {
        var controller = new PlaybackSyncController();
        controller.Step(localPosition: 101.0, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        var decision = controller.Step(localPosition: 90.0, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        Assert.True(decision.Seek);
        Assert.Equal(100.0, decision.SeekTarget);
        Assert.True(decision.SpeedChanged);
        Assert.Equal(1d, decision.Speed);
        Assert.Equal(1d, controller.AppliedSpeed);
    }

    [Fact]
    public void ControllerSettlesAfterASeekBeforeCorrectingAgain()
    {
        var controller = new PlaybackSyncController();
        controller.Step(localPosition: 90.0, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        var duringSettle = controller.Step(localPosition: 90.0, targetPosition: 100.1, durationSeconds: 600, false,
            (float)PlaybackSyncController.SeekSettleSeconds / 2f);
        Assert.Equal(SyncDecision.Hold, duringSettle);

        var afterSettle = controller.Step(localPosition: 90.0, targetPosition: 100.2, durationSeconds: 600, false,
            (float)PlaybackSyncController.SeekSettleSeconds);
        Assert.True(afterSettle.Seek);
    }

    [Fact]
    public void ControllerWaitsWhileThePlayerIsSeeking()
    {
        var controller = new PlaybackSyncController();

        var decision = controller.Step(localPosition: 90.0, targetPosition: 100.0, durationSeconds: 600, true, Tick);

        Assert.Equal(SyncDecision.Hold, decision);
    }

    [Fact]
    public void ControllerNeverSeeksIntoTheEnd()
    {
        var controller = new PlaybackSyncController();

        var decision = controller.Step(localPosition: 590.0, targetPosition: 599.8, durationSeconds: 600, false, Tick);

        Assert.False(decision.Seek);
        Assert.False(decision.SpeedChanged);
    }

    [Fact]
    public void ControllerReturnsToNormalSpeedInsideTheDeadZone()
    {
        var controller = new PlaybackSyncController();
        controller.Step(localPosition: 101.0, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        var decision = controller.Step(localPosition: 100.1, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        Assert.True(decision.SpeedChanged);
        Assert.Equal(1d, decision.Speed);
    }

    [Fact]
    public void ReleaseReportsOnlyWhenSpeedWasNudged()
    {
        var controller = new PlaybackSyncController();
        Assert.False(controller.Release());

        controller.Step(localPosition: 101.0, targetPosition: 100.0, durationSeconds: 600, false, Tick);

        Assert.True(controller.Release());
        Assert.Equal(1d, controller.AppliedSpeed);
        Assert.False(controller.Release());
    }

    [Fact]
    public void AJoinerWhoseSeeksTakeLongerThanTheToleranceStillCatchesUp()
    {
        var result = SimulateJoin(seekSeconds: 4d, seekLands: true);

        Assert.Equal(2, result.Seeks);
        Assert.Equal(0, result.Reopens);
        Assert.InRange(Math.Abs(result.FinalDrift), 0d, PlaybackSyncController.HardSeekToleranceSeconds);
    }

    [Fact]
    public void AJoinerWhosePlayerReportsTheSeekATickLateStillCatchesUp()
    {
        var result = SimulateJoin(seekSeconds: 4d, seekLands: true, seekStartLagTicks: 1);

        Assert.Equal(2, result.Seeks);
        Assert.Equal(0, result.Reopens);
        Assert.InRange(Math.Abs(result.FinalDrift), 0d, PlaybackSyncController.HardSeekToleranceSeconds);
    }

    [Fact]
    public void AJoinerWhoseSeeksGoNowhereReopensTheLinkInsteadOfSeekingForever()
    {
        var result = SimulateJoin(seekSeconds: 0d, seekLands: false);

        Assert.Equal(2, result.Seeks);
        Assert.Equal(1, result.Reopens);
        Assert.InRange(Math.Abs(result.FinalDrift), 0d, PlaybackSyncController.HardSeekToleranceSeconds);
    }

    [Fact]
    public void AViewerFollowsAHostSkippingAheadSeveralTimesWithoutReopening()
    {
        var result = SimulateJoin(seekSeconds: 0.2d, seekLands: true, hostSkips: 3, hostSkipEverySeconds: 1.8d);

        Assert.Equal(0, result.Reopens);
        Assert.InRange(Math.Abs(result.FinalDrift), 0d, PlaybackSyncController.HardSeekToleranceSeconds);
    }

    [Fact]
    public void TheFirstHardSeekAfterCatchingUpAimsAtTheHostItself()
    {
        var controller = new PlaybackSyncController();
        controller.Step(localPosition: 600.0, targetPosition: 607.5, durationSeconds: 7200, false, Tick);
        controller.Step(localPosition: 600.0, targetPosition: 609.5, durationSeconds: 7200, true, 2f);
        controller.Step(localPosition: 607.5, targetPosition: 609.5, durationSeconds: 7200, false, Tick);
        controller.Step(localPosition: 609.5, targetPosition: 609.5, durationSeconds: 7200, false, Tick);
        Assert.InRange(controller.SeekLead, 1.9d, 2.1d);

        var decision = controller.Step(localPosition: 609.5, targetPosition: 619.5, durationSeconds: 7200, false, Tick);

        Assert.True(decision.Seek);
        Assert.Equal(619.5, decision.SeekTarget);
    }

    private static (int Seeks, int Reopens, double FinalDrift) SimulateJoin(double seekSeconds, bool seekLands,
        int seekStartLagTicks = 0, int hostSkips = 0, double hostSkipEverySeconds = 0d)
    {
        const double hostStart = 607.5;
        const double hostSkipSeconds = 10d;
        var controller = new PlaybackSyncController();
        var local = 600d;
        var hostOffset = 0d;
        var skipsDone = 0;
        var elapsed = 0d;
        var lagLeft = 0;
        var seekingLeft = 0d;
        var landing = 0d;
        var seeks = 0;
        var reopens = 0;
        for (var tick = 0; tick < 120 * 60; tick++)
        {
            elapsed += Tick;
            if (skipsDone < hostSkips && elapsed >= 5d + skipsDone * hostSkipEverySeconds)
            {
                skipsDone++;
                hostOffset += hostSkipSeconds;
            }

            var host = hostStart + elapsed + hostOffset;
            var seeking = false;
            if (lagLeft > 0)
            {
                lagLeft--;
                local += Tick;
            }
            else if (seekingLeft > 0d)
            {
                seekingLeft -= Tick;
                seeking = seekingLeft > 0d;
                if (!seeking)
                {
                    local = landing;
                }
            }
            else
            {
                local += Tick;
            }

            var decision = controller.Step(local, host, durationSeconds: 7200, seeking, Tick);
            if (decision.Reopen)
            {
                reopens++;
                controller.Reset();
                local = host;
                continue;
            }

            if (decision.Seek)
            {
                seeks++;
                if (seekLands)
                {
                    lagLeft = seekStartLagTicks;
                    seekingLeft = seekSeconds;
                    landing = decision.SeekTarget;
                }
            }
        }

        return (seeks, reopens, local - (hostStart + elapsed + hostOffset));
    }
}
