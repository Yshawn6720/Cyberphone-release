using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Calendar;

internal enum CalendarReminderDue : byte
{
    None,
    Notify,
    Skip,
}

internal static class CalendarReminder
{
    public const int None = -1;
    public const int AtEventTime = 0;
    public const int LateGraceMinutes = 60;
    private const int AtEventTimeIndex = 1;

    public static ReadOnlySpan<int> LeadOptionsMinutes => new[] { None, AtEventTime, 5, 10, 15, 30, 60, 120, 1440 };

    private static readonly LocString[] LeadOptionLabels =
    {
        L.Calendar.AlertNone, L.Calendar.AlertAtTime, L.Calendar.AlertMinutes5, L.Calendar.AlertMinutes10,
        L.Calendar.AlertMinutes15, L.Calendar.AlertMinutes30, L.Calendar.AlertHour1, L.Calendar.AlertHours2,
        L.Calendar.AlertDay1,
    };

    private static readonly LocString[] RepeatLabels =
    {
        L.Calendar.RepeatNever, L.Calendar.RepeatDaily, L.Calendar.RepeatWeekly, L.Calendar.RepeatBiweekly,
        L.Calendar.RepeatMonthly,
    };

    public static int RepeatOptionCount => RepeatLabels.Length;

    public static LocString RepeatLabel(CalendarRepeat repeat) =>
        RepeatLabels[Math.Clamp((int)repeat, 0, RepeatLabels.Length - 1)];

    public static int LeadIndexOf(int minutesBefore)
    {
        for (var index = 0; index < LeadOptionsMinutes.Length; index++)
        {
            if (LeadOptionsMinutes[index] == minutesBefore)
            {
                return index;
            }
        }

        return AtEventTimeIndex;
    }

    public static LocString LeadLabelAt(int leadIndex) => LeadOptionLabels[leadIndex];

    public static bool TryFireTime(CalendarCustomEvent calendarEvent, out DateTime fireTime) =>
        TryFireTime(calendarEvent, calendarEvent.When, out fireTime);

    public static bool TryFireTime(CalendarCustomEvent calendarEvent, DateTime occurrence, out DateTime fireTime)
    {
        if (calendarEvent.ReminderMinutesBefore == None)
        {
            fireTime = default;
            return false;
        }

        fireTime = occurrence.AddMinutes(-LeadMinutes(calendarEvent));
        return true;
    }

    public static CalendarReminderDue Due(CalendarCustomEvent calendarEvent, DateTime now, out DateTime occurrence)
    {
        occurrence = calendarEvent.When;
        if (calendarEvent.ReminderMinutesBefore == None)
        {
            return CalendarReminderDue.None;
        }

        if (calendarEvent.Repeat == CalendarRepeat.None)
        {
            if (calendarEvent.Notified || !TryFireTime(calendarEvent, out var fireTime) || fireTime > now)
            {
                return CalendarReminderDue.None;
            }

            return CalendarReminderDue.Notify;
        }

        var index = CalendarRecurrence.IndexAtOrBefore(calendarEvent.When, calendarEvent.Repeat,
            now.AddMinutes(LeadMinutes(calendarEvent)));
        if (index == CalendarRecurrence.NoOccurrence)
        {
            return CalendarReminderDue.None;
        }

        occurrence = CalendarRecurrence.Occurrence(calendarEvent.When, calendarEvent.Repeat, index);
        if (occurrence <= calendarEvent.LastNotifiedOccurrence)
        {
            return CalendarReminderDue.None;
        }

        return calendarEvent.EndOf(occurrence).AddMinutes(LateGraceMinutes) < now
            ? CalendarReminderDue.Skip
            : CalendarReminderDue.Notify;
    }

    public static void MarkHandled(CalendarCustomEvent calendarEvent, DateTime occurrence)
    {
        if (calendarEvent.Repeat == CalendarRepeat.None)
        {
            calendarEvent.Notified = true;
            return;
        }

        calendarEvent.LastNotifiedOccurrence = occurrence;
    }

    public static void Arm(CalendarCustomEvent calendarEvent, DateTime now, bool created)
    {
        if (calendarEvent.Repeat != CalendarRepeat.None)
        {
            var index = CalendarRecurrence.IndexAtOrBefore(calendarEvent.When, calendarEvent.Repeat, now);
            var latestPassed = index == CalendarRecurrence.NoOccurrence
                ? DateTime.MinValue
                : CalendarRecurrence.Occurrence(calendarEvent.When, calendarEvent.Repeat, index);
            calendarEvent.LastNotifiedOccurrence = created || latestPassed > calendarEvent.LastNotifiedOccurrence
                ? latestPassed
                : calendarEvent.LastNotifiedOccurrence;
            return;
        }

        if (created)
        {
            calendarEvent.Notified = calendarEvent.When <= now;
            return;
        }

        if (TryFireTime(calendarEvent, out var fireTime) && fireTime > now)
        {
            calendarEvent.Notified = false;
            return;
        }

        if (calendarEvent.When <= now)
        {
            calendarEvent.Notified = true;
        }
    }

    public static string Body(DateTime when, DateTime now)
    {
        if (when.Date == now.Date)
        {
            return TimeText.Clock(when);
        }

        return string.Concat(when.ToString("MMM d", Loc.Culture), ", ", TimeText.Clock(when));
    }

    private static int LeadMinutes(CalendarCustomEvent calendarEvent) =>
        Math.Max(AtEventTime, calendarEvent.ReminderMinutesBefore);
}
