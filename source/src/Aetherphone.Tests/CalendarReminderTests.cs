using Aetherphone.Core.Calendar;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CalendarReminderTests
{
    private static readonly DateTime When = new(2026, 9, 26, 18, 30, 0);

    [Fact]
    public void AtEventTimeFiresAtTheEventItself()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = CalendarReminder.AtEventTime };

        Assert.True(CalendarReminder.TryFireTime(calendarEvent, out var fireTime));
        Assert.Equal(When, fireTime);
    }

    [Fact]
    public void LeadTimeMovesTheFireTimeEarlier()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = 30 };

        Assert.True(CalendarReminder.TryFireTime(calendarEvent, out var fireTime));
        Assert.Equal(When.AddMinutes(-30), fireTime);
    }

    [Fact]
    public void NoneNeverFires()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = CalendarReminder.None };

        Assert.False(CalendarReminder.TryFireTime(calendarEvent, out _));
    }

    [Fact]
    public void UnknownLeadValuesFallBackToTheEventTimeOption()
    {
        var index = CalendarReminder.LeadIndexOf(45);

        Assert.Equal(CalendarReminder.AtEventTime, CalendarReminder.LeadOptionsMinutes[index]);
    }

    [Fact]
    public void EveryLeadOptionRoundTripsThroughItsIndex()
    {
        var options = CalendarReminder.LeadOptionsMinutes;
        for (var index = 0; index < options.Length; index++)
        {
            Assert.Equal(index, CalendarReminder.LeadIndexOf(options[index]));
        }
    }

    [Fact]
    public void WeeklyEventNotifiesEachOccurrenceOnce()
    {
        var calendarEvent = new CalendarCustomEvent
        {
            When = When, Repeat = CalendarRepeat.Weekly, ReminderMinutesBefore = 15,
        };
        CalendarReminder.Arm(calendarEvent, When.AddDays(-1), true);
        var secondLead = When.AddDays(7).AddMinutes(-10);

        Assert.Equal(CalendarReminderDue.None, CalendarReminder.Due(calendarEvent, When.AddMinutes(-20), out _));
        Assert.Equal(CalendarReminderDue.Notify, CalendarReminder.Due(calendarEvent, When.AddMinutes(-10), out var first));
        Assert.Equal(When, first);
        CalendarReminder.MarkHandled(calendarEvent, first);
        Assert.Equal(CalendarReminderDue.None, CalendarReminder.Due(calendarEvent, When.AddMinutes(5), out _));
        Assert.Equal(CalendarReminderDue.Notify, CalendarReminder.Due(calendarEvent, secondLead, out var second));
        Assert.Equal(When.AddDays(7), second);
    }

    [Fact]
    public void ArmingARepeatingEventSkipsOccurrencesThatAlreadyStarted()
    {
        var calendarEvent = new CalendarCustomEvent
        {
            When = When, Repeat = CalendarRepeat.Daily, ReminderMinutesBefore = CalendarReminder.AtEventTime,
        };

        CalendarReminder.Arm(calendarEvent, When.AddDays(3).AddHours(1), true);

        Assert.Equal(When.AddDays(3), calendarEvent.LastNotifiedOccurrence);
        Assert.Equal(CalendarReminderDue.None,
            CalendarReminder.Due(calendarEvent, When.AddDays(3).AddHours(2), out _));
    }

    [Fact]
    public void LongMissedRepeatingRemindersAreSkippedSilently()
    {
        var calendarEvent = new CalendarCustomEvent
        {
            When = When, Repeat = CalendarRepeat.Weekly, ReminderMinutesBefore = CalendarReminder.AtEventTime,
            DurationMinutes = 60,
        };
        CalendarReminder.Arm(calendarEvent, When.AddDays(-1), true);
        var late = When.AddMinutes(60 + CalendarReminder.LateGraceMinutes + 1);

        Assert.Equal(CalendarReminderDue.Skip, CalendarReminder.Due(calendarEvent, late, out var occurrence));
        Assert.Equal(When, occurrence);
    }

    [Fact]
    public void CreatingAPastOneOffEventDoesNotNotify()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = 30 };

        CalendarReminder.Arm(calendarEvent, When.AddHours(1), true);

        Assert.Equal(CalendarReminderDue.None, CalendarReminder.Due(calendarEvent, When.AddHours(1), out _));
    }

    [Fact]
    public void EditingAOneOffEventIntoTheFutureRearmsIt()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = 30, Notified = true };
        calendarEvent.When = When.AddDays(1);

        CalendarReminder.Arm(calendarEvent, When, false);

        Assert.False(calendarEvent.Notified);
    }

    [Fact]
    public void EditingAWeeklyEventAfterItsAlertDoesNotRingAgain()
    {
        var calendarEvent = new CalendarCustomEvent
        {
            When = When, Repeat = CalendarRepeat.Weekly, ReminderMinutesBefore = 30,
        };
        CalendarReminder.Arm(calendarEvent, When.AddDays(-1), true);
        Assert.Equal(CalendarReminderDue.Notify, CalendarReminder.Due(calendarEvent, When.AddMinutes(-30), out var first));
        CalendarReminder.MarkHandled(calendarEvent, first);

        CalendarReminder.Arm(calendarEvent, When.AddMinutes(-20), false);

        Assert.Equal(CalendarReminderDue.None, CalendarReminder.Due(calendarEvent, When.AddMinutes(-19), out _));
    }

    [Fact]
    public void StoppingARepeatOnAPastSeriesDoesNotNotifyTheOldStart()
    {
        var calendarEvent = new CalendarCustomEvent
        {
            When = When, Repeat = CalendarRepeat.Daily, ReminderMinutesBefore = 15,
        };
        CalendarReminder.Arm(calendarEvent, When.AddDays(-1), true);
        var now = When.AddDays(21).AddHours(-3);

        calendarEvent.Repeat = CalendarRepeat.None;
        CalendarReminder.Arm(calendarEvent, now, false);

        Assert.Equal(CalendarReminderDue.None, CalendarReminder.Due(calendarEvent, now, out _));
    }

    [Fact]
    public void AddingAnAlertToAnUpcomingEventStillNotifies()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = CalendarReminder.None };
        var now = When.AddHours(-2);
        CalendarReminder.Arm(calendarEvent, now, true);

        calendarEvent.ReminderMinutesBefore = 1440;
        CalendarReminder.Arm(calendarEvent, now, false);

        Assert.Equal(CalendarReminderDue.Notify, CalendarReminder.Due(calendarEvent, now, out _));
    }
}
