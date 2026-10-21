using Aetherphone.Core.Clock;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ClockLogicTests
{
    private static readonly DateTime Utc = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(5);

    [Fact]
    public void AnEorzeanDayLastsSeventyMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(70), EorzeaClock.RealDay);
    }

    [Fact]
    public void EorzeaConversionRoundTrips()
    {
        var back = EorzeaClock.UtcAt(EorzeaClock.SecondsAt(Utc));

        Assert.True((back - Utc).Duration() <= Tolerance);
    }

    [Fact]
    public void NextBellIsAFullBellAwayOnTheBoundary()
    {
        var bell = Math.Floor(EorzeaClock.SecondsAt(Utc) / EorzeaClock.SecondsPerBell) * EorzeaClock.SecondsPerBell;
        var onBoundary = EorzeaClock.UtcAt(bell);

        var until = EorzeaClock.UntilNextBell(onBoundary);

        Assert.True((until - TimeSpan.FromSeconds(175)).Duration() <= Tolerance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5 * 60 + 30)]
    [InlineData(23 * 60 + 59)]
    public void NextEorzeaOccurrenceLandsOnTheRequestedMinute(int minuteOfDay)
    {
        var next = EorzeaClock.NextUtc(minuteOfDay, Utc);

        Assert.True(next > Utc);
        Assert.True(next - Utc <= EorzeaClock.RealDay);
        var secondOfDay = EorzeaClock.SecondsAt(next) % EorzeaClock.SecondsPerDay;
        Assert.True(Math.Abs(secondOfDay - minuteOfDay * 60d) < 0.5d);
    }

    [Fact]
    public void EorzeaAlarmIsDueRightAfterItsBell()
    {
        var alarm = new AlarmEntry { Eorzea = true, Hour = 14, Minute = 0 };
        var occurrence = EorzeaClock.NextUtc(alarm.MinuteOfDay, Utc);

        var now = occurrence.AddSeconds(30);
        var due = AlarmSchedule.TryResolveDue(alarm, now.ToLocalTime(), now, out var dueUtc, out var key);

        Assert.True(due);
        Assert.True((dueUtc - occurrence).Duration() <= Tolerance);
        Assert.True(key < 0);
    }

    [Fact]
    public void EorzeaAlarmIsNotDueOnceTheCatchUpWindowPasses()
    {
        var alarm = new AlarmEntry { Eorzea = true, Hour = 14, Minute = 0 };
        var occurrence = EorzeaClock.NextUtc(alarm.MinuteOfDay, Utc);
        var late = occurrence + AlarmSchedule.CatchUpWindow + TimeSpan.FromSeconds(1);

        Assert.False(AlarmSchedule.TryResolveDue(alarm, late.ToLocalTime(), late, out _, out _));
    }

    [Fact]
    public void EorzeaAlarmKeysDifferFromOneDayToTheNext()
    {
        var alarm = new AlarmEntry { Eorzea = true, Hour = 6, Minute = 0 };
        var first = EorzeaClock.NextUtc(alarm.MinuteOfDay, Utc);
        var second = EorzeaClock.NextUtc(alarm.MinuteOfDay, first.AddSeconds(1));

        AlarmSchedule.TryResolveDue(alarm, first.ToLocalTime(), first.AddSeconds(1), out _, out var firstKey);
        AlarmSchedule.TryResolveDue(alarm, second.ToLocalTime(), second.AddSeconds(1), out _, out var secondKey);

        Assert.NotEqual(firstKey, secondKey);
    }

    [Fact]
    public void LocalAlarmIsDueWithinTheCatchUpWindow()
    {
        var alarm = new AlarmEntry { Hour = 7, Minute = 0 };
        var nowLocal = new DateTime(2026, 10, 5, 7, 4, 0);

        var due = AlarmSchedule.TryResolveDue(alarm, nowLocal, Utc, out var dueUtc, out var key);

        Assert.True(due);
        Assert.Equal(AlarmSchedule.MinuteKey(new DateTime(2026, 10, 5, 7, 0, 0)), key);
        Assert.Equal(Utc - TimeSpan.FromMinutes(4), dueUtc);
    }

    [Fact]
    public void LocalAlarmSkipsDaysItDoesNotRepeatOn()
    {
        var alarm = new AlarmEntry { Hour = 7, Minute = 0 };
        alarm.ToggleDay(DayOfWeek.Monday);
        var tuesday = new DateTime(2026, 10, 6, 7, 2, 0);

        Assert.False(AlarmSchedule.TryResolveDue(alarm, tuesday, Utc, out _, out _));
    }

    [Fact]
    public void ArmingAnAlarmForAMomentJustPassedKeepsItFromRingingNow()
    {
        var alarm = new AlarmEntry { Hour = 7, Minute = 0 };
        var nowLocal = new DateTime(2026, 10, 5, 7, 3, 0);

        AlarmSchedule.Arm(alarm, nowLocal, Utc);

        Assert.Equal(AlarmSchedule.MinuteKey(new DateTime(2026, 10, 5, 7, 0, 0)), alarm.LastFiredEpochMinute);
    }

    [Fact]
    public void ArmingAnAlarmLaterInTheDayClearsTheFiredKey()
    {
        var alarm = new AlarmEntry { Hour = 9, Minute = 0, LastFiredEpochMinute = 42 };

        AlarmSchedule.Arm(alarm, new DateTime(2026, 10, 5, 7, 3, 0), Utc);

        Assert.Equal(0, alarm.LastFiredEpochMinute);
    }

    [Fact]
    public void OneShotLocalAlarmInThePastMovesToTomorrow()
    {
        var alarm = new AlarmEntry { Hour = 6, Minute = 30 };
        var nowLocal = new DateTime(2026, 10, 5, 8, 0, 0);

        Assert.Equal(new DateTime(2026, 10, 6, 6, 30, 0), AlarmSchedule.NextLocal(alarm, nowLocal));
    }

    [Fact]
    public void SnoozeLengthIsClampedToTheSupportedRange()
    {
        Assert.Equal(TimeSpan.FromMinutes(AlarmSchedule.MaxSnoozeMinutes),
            new AlarmEntry { SnoozeMinutes = 99 }.SnoozeLength);
        Assert.Equal(TimeSpan.Zero, new AlarmEntry { SnoozeMinutes = -3 }.SnoozeLength);
        Assert.Equal(TimeSpan.FromMinutes(9), new AlarmEntry().SnoozeLength);
    }

    [Fact]
    public void CountdownPausesAndResumesWithTheSameRemainingTime()
    {
        var configuration = new Configuration();
        CountdownTimer.Start(configuration, 300, "Tea", Utc);

        CountdownTimer.Pause(configuration, Utc.AddSeconds(100.4));

        Assert.Equal(CountdownPhase.Paused, CountdownTimer.Phase(configuration, Utc.AddSeconds(500), out var paused));
        Assert.Equal(200d, paused);
        Assert.Null(configuration.TimerEndsAtUtc);

        CountdownTimer.Resume(configuration, Utc.AddSeconds(1000));

        Assert.Equal(CountdownPhase.Running,
            CountdownTimer.Phase(configuration, Utc.AddSeconds(1000), out var remaining));
        Assert.Equal(200d, remaining, 3);
        Assert.Equal("Tea", configuration.TimerLabel);
    }

    [Fact]
    public void CountdownFinishesAndCancelReturnsToIdle()
    {
        var configuration = new Configuration();
        CountdownTimer.Start(configuration, 60, string.Empty, Utc);

        Assert.Equal(CountdownPhase.Finished, CountdownTimer.Phase(configuration, Utc.AddSeconds(61), out _));

        CountdownTimer.Cancel(configuration);

        Assert.Equal(CountdownPhase.Idle, CountdownTimer.Phase(configuration, Utc, out _));
    }

    [Fact]
    public void RecentTimersStayUniqueNewestFirstAndCapped()
    {
        var recents = new List<int>();
        for (var seconds = 1; seconds <= CountdownTimer.RecentCapacity + 2; seconds++)
        {
            CountdownTimer.Remember(recents, seconds * 60);
        }

        CountdownTimer.Remember(recents, 5 * 60);

        Assert.Equal(CountdownTimer.RecentCapacity, recents.Count);
        Assert.Equal(5 * 60, recents[0]);
        Assert.Single(recents, value => value == 5 * 60);
    }

    [Fact]
    public void LapBoardFindsTheBestAndWorstSplits()
    {
        var cumulative = new List<double> { 1000, 3500, 4200, 6000 };

        LapBoard.Extremes(cumulative, out var best, out var worst);

        Assert.Equal(2, best);
        Assert.Equal(1, worst);
    }

    [Fact]
    public void LapBoardHighlightsNothingForASingleLapOrEqualSplits()
    {
        LapBoard.Extremes(new List<double> { 1000 }, out var singleBest, out var singleWorst);
        LapBoard.Extremes(new List<double> { 1000, 2000, 3000 }, out var evenBest, out var evenWorst);

        Assert.Equal(-1, singleBest);
        Assert.Equal(-1, singleWorst);
        Assert.Equal(-1, evenBest);
        Assert.Equal(-1, evenWorst);
    }

    [Fact]
    public void ReorderMovesAnItemAndClampsTheTarget()
    {
        var items = new List<string> { "a", "b", "c", "d" };

        Assert.True(ClockReorder.Move(items, 0, 2));
        Assert.Equal(new[] { "b", "c", "a", "d" }, items);
        Assert.True(ClockReorder.Move(items, 3, -5));
        Assert.Equal(new[] { "d", "b", "c", "a" }, items);
        Assert.False(ClockReorder.Move(items, 1, 1));
        Assert.False(ClockReorder.Move(items, 9, 0));
    }

    [Fact]
    public void RingerHonoursAPerAlarmSnoozeLength()
    {
        var ringer = new AlarmRinger(_ => { }, () => { });
        ringer.Ring(AlarmRingKind.Alarm, "Gather", Utc, TimeSpan.FromMinutes(3));

        ringer.Snooze(Utc);

        Assert.Equal(Utc.AddMinutes(3), ringer.SnoozeEndsUtc);
    }

    [Fact]
    public void RingerCannotSnoozeWhenSnoozeIsOff()
    {
        var ringer = new AlarmRinger(_ => { }, () => { });
        ringer.Ring(AlarmRingKind.Alarm, "Gather", Utc, TimeSpan.Zero);

        Assert.False(ringer.CanSnooze);
    }
}
