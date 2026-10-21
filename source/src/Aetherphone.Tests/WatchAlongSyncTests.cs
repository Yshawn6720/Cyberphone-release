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
}
