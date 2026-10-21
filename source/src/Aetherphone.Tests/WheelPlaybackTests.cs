using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelPlaybackTests
{
    private static CasinoRoomSnapshotDto Snapshot(int phase, long roundIndex)
    {
        return new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.WheelFloor,
            GameKind: CasinoWire.WheelKind,
            Phase: phase,
            RoundIndex: roundIndex);
    }

    private static CasinoWheelRoomStateDto Board(long roundIndex, int segment = -1)
    {
        return new CasinoWheelRoomStateDto(RoundIndex: roundIndex, Segment: segment);
    }

    [Fact]
    public void AnOpenRoomRestsOnTheRimAndCallsItBetting()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Open, 1), Board(1), 42_000, 0.016f);
        Assert.Equal(WheelStage.Betting, playback.Stage);
        Assert.Equal("wheel-floor#1", playback.RoundId);
        Assert.Equal(0f, playback.Angle);
        Assert.Equal(-1, playback.Segment);
    }

    [Fact]
    public void TheLockWithoutAResultYetTurnsTheRimAndCallsItLocking()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Open, 1), Board(1), 42_000, 0.016f);
        playback.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1), 8_000, 0.5f);
        Assert.Equal(WheelStage.Locking, playback.Stage);
        Assert.True(playback.Angle > 0f);
        Assert.Equal(-1, playback.Segment);
    }

    [Fact]
    public void AResultDuringALongLockStartsTheSpinBeforeTheCurveIsDue()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Open, 1), Board(1), 42_000, 0.016f);
        playback.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1, 17), 8_000, 0.016f);
        Assert.Equal(WheelStage.Spinning, playback.Stage);
        Assert.Equal(17, playback.Segment);
        Assert.Equal(0f, playback.SpinProgress);
    }

    [Fact]
    public void JoiningMidSpinSkipsToWhereTheRoomAlreadyIs()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1, 23), 3_000, 0f);

        Assert.Equal(WheelStage.Spinning, playback.Stage);
        Assert.Equal(23, playback.Segment);
        var expected = (WheelChoreography.SpinSeconds - (3f - WheelRoundPlayback.SettleHoldSeconds))
            / WheelChoreography.SpinSeconds;
        Assert.Equal(expected, playback.SpinProgress, 3);
        Assert.True(playback.SpinProgress > 0.5f);
        Assert.True(playback.SpinProgress < 1f);
    }

    [Fact]
    public void AWatcherAndAMidSpinJoinerStopOnTheSameWedge()
    {
        var watcher = new WheelRoundPlayback();
        watcher.Update(Snapshot(CasinoRoomPhases.Open, 1), Board(1), 20_000, 0.016f);
        watcher.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1, 41), 8_000, 0.016f);
        for (var step = 0; step < 600; step++)
        {
            watcher.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1, 41), 8_000, 0.02f);
        }

        var joiner = new WheelRoundPlayback();
        joiner.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1, 41), 2_000, 0f);
        for (var step = 0; step < 600; step++)
        {
            joiner.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1, 41), 2_000, 0.02f);
        }

        Assert.Equal(WheelStage.Settling, watcher.Stage);
        Assert.Equal(WheelStage.Settling, joiner.Stage);
        Assert.Equal(41, WheelChoreography.SegmentUnderPointer(watcher.Angle));
        Assert.Equal(41, WheelChoreography.SegmentUnderPointer(joiner.Angle));
    }

    [Fact]
    public void JoiningDuringTheResultWindowShowsTheWheelAlreadyStopped()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Result, 1), Board(1, 8), 6_000, 0f);

        Assert.Equal(WheelStage.Settling, playback.Stage);
        Assert.Equal(8, playback.Segment);
        Assert.Equal(1f, playback.SpinProgress);
        Assert.Equal(8, WheelChoreography.SegmentUnderPointer(playback.Angle));
    }

    [Fact]
    public void ANewRoundReturnsToBettingAndLeavesTheRimWhereItStopped()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Result, 1), Board(1, 31), 6_000, 0f);
        var settledAngle = playback.Angle;

        playback.Update(Snapshot(CasinoRoomPhases.Open, 2), Board(2), 60_000, 0.016f);

        Assert.Equal(WheelStage.Betting, playback.Stage);
        Assert.Equal("wheel-floor#2", playback.RoundId);
        Assert.Equal(-1, playback.Segment);
        Assert.Equal(31, playback.LastSegment);
        Assert.Equal(WheelChoreography.Normalize(settledAngle), playback.Angle, 4);
        Assert.Equal(31, WheelChoreography.SegmentUnderPointer(playback.Angle));
    }

    [Fact]
    public void TheNextRoundSpinsToItsOwnWedgeFromWhereTheLastOneRested()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Result, 1), Board(1, 5), 6_000, 0f);
        playback.Update(Snapshot(CasinoRoomPhases.Open, 2), Board(2), 60_000, 0.016f);
        playback.Update(Snapshot(CasinoRoomPhases.Locked, 2), Board(2, 44), 8_000, 0.016f);
        for (var step = 0; step < 700; step++)
        {
            playback.Update(Snapshot(CasinoRoomPhases.Locked, 2), Board(2, 44), 8_000, 0.02f);
        }

        Assert.Equal(WheelStage.Settling, playback.Stage);
        Assert.Equal(44, WheelChoreography.SegmentUnderPointer(playback.Angle));
    }

    [Fact]
    public void ADroppedSnapshotHoldsTheFrameInsteadOfRewindingTheWheel()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1, 12), 3_000, 0f);
        var held = playback.Angle;

        playback.Update(null, null, 0, 0.2f);

        Assert.Equal(held, playback.Angle);
        Assert.Equal(WheelStage.Spinning, playback.Stage);
    }

    [Fact]
    public void AnUnreadableDrawIsIgnoredRatherThanSpunTo()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Locked, 1), Board(1, WheelRules.SegmentCount), 3_000, 0.016f);
        Assert.Equal(WheelStage.Locking, playback.Stage);
        Assert.Equal(-1, playback.Segment);

        Assert.Equal(-1, WheelRoundPlayback.DrawnSegment(null));
        Assert.Equal(-1, WheelRoundPlayback.DrawnSegment(Board(1)));
        Assert.Equal(-1, WheelRoundPlayback.DrawnSegment(Board(1, -2)));
        Assert.Equal(-1, WheelRoundPlayback.DrawnSegment(Board(1, WheelRules.SegmentCount)));
        Assert.Equal(0, WheelRoundPlayback.DrawnSegment(Board(1, 0)));
    }

    [Fact]
    public void TheRoundKeyIsTheRoomAndItsIndex()
    {
        Assert.Equal("wheel-floor#7", WheelRoundPlayback.RoundKeyOf(Snapshot(CasinoRoomPhases.Open, 7)));
        Assert.NotEqual(WheelRoundPlayback.RoundKeyOf(Snapshot(CasinoRoomPhases.Open, 7)),
            WheelRoundPlayback.RoundKeyOf(Snapshot(CasinoRoomPhases.Open, 8)));
    }

    [Fact]
    public void TheSpinAnchorReadsTheDeadlineRatherThanTheMomentOfArrival()
    {
        Assert.Equal(WheelChoreography.SpinSeconds,
            WheelRoundPlayback.InitialSpinElapsed(CasinoRoomPhases.Result, 6_000));
        Assert.Equal(WheelChoreography.SpinSeconds,
            WheelRoundPlayback.InitialSpinElapsed(CasinoRoomPhases.Locked, 0));
        Assert.Equal(WheelChoreography.SpinSeconds - (2f - WheelRoundPlayback.SettleHoldSeconds),
            WheelRoundPlayback.InitialSpinElapsed(CasinoRoomPhases.Locked, 2_000), 4);
        Assert.True(WheelRoundPlayback.InitialSpinElapsed(CasinoRoomPhases.Locked, 8_000) < 0f);
    }

    [Fact]
    public void ResetReturnsTheCabinetToAColdWheel()
    {
        var playback = new WheelRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Result, 1), Board(1, 19), 6_000, 0f);
        playback.Reset();

        Assert.Equal(WheelStage.Waiting, playback.Stage);
        Assert.Equal(string.Empty, playback.RoundId);
        Assert.Equal(0f, playback.Angle);
        Assert.Equal(-1, playback.Segment);
        Assert.Equal(-1, playback.LastSegment);
    }
}
