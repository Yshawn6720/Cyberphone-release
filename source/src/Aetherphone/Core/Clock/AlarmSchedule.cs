using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Clock;

internal static class AlarmSchedule
{
    public const int DefaultSnoozeMinutes = 9;
    public const int MaxSnoozeMinutes = 15;
    public const byte EveryDayMask = 0x7F;

    public static readonly TimeSpan CatchUpWindow = TimeSpan.FromMinutes(10);

    private const byte WeekdayMask =
        (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Tuesday) | (1 << (int)DayOfWeek.Wednesday) |
        (1 << (int)DayOfWeek.Thursday) | (1 << (int)DayOfWeek.Friday);

    private const byte WeekendMask = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);

    private static readonly DayOfWeek[] DisplayOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    public static long MinuteKey(DateTime localTime) =>
        (localTime.Year * 366L + localTime.DayOfYear) * 1440L + localTime.Hour * 60L + localTime.Minute;

    public static long EorzeaKey(double eorzeaSeconds) => -1L - EorzeaClock.DayIndex(eorzeaSeconds);

    public static string RepeatLabel(AlarmEntry alarm)
    {
        if (alarm.Eorzea)
        {
            return Loc.T(alarm.Repeats ? L.Clock.RepeatEorzeaDaily : L.Clock.RepeatNever);
        }

        if (alarm.RepeatDays == 0)
        {
            return Loc.T(L.Clock.RepeatNever);
        }

        if (alarm.RepeatDays == EveryDayMask)
        {
            return Loc.T(L.Clock.RepeatEveryDay);
        }

        if (alarm.RepeatDays == WeekdayMask)
        {
            return Loc.T(L.Clock.RepeatWeekdays);
        }

        if (alarm.RepeatDays == WeekendMask)
        {
            return Loc.T(L.Clock.RepeatWeekends);
        }

        var abbreviations = Loc.Culture.DateTimeFormat.AbbreviatedDayNames;
        var names = new List<string>(7);
        for (var index = 0; index < DisplayOrder.Length; index++)
        {
            var day = DisplayOrder[index];
            if (alarm.RepeatsOn(day))
            {
                names.Add(abbreviations[(int)day]);
            }
        }

        return string.Join(", ", names);
    }

    public static DateTime NextOccurrence(AlarmEntry alarm, DateTime nowLocal) =>
        alarm.Eorzea
            ? EorzeaClock.NextUtc(alarm.MinuteOfDay, nowLocal.ToUniversalTime()).ToLocalTime()
            : NextLocal(alarm, nowLocal);

    public static DateTime NextLocal(AlarmEntry alarm, DateTime nowLocal)
    {
        var today = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, alarm.Hour, alarm.Minute, 0,
            nowLocal.Kind);
        if (alarm.RepeatDays == 0)
        {
            return today > nowLocal ? today : today.AddDays(1);
        }

        for (var offset = 0; offset < 8; offset++)
        {
            var candidate = today.AddDays(offset);
            if (alarm.RepeatsOn(candidate.DayOfWeek) && candidate > nowLocal)
            {
                return candidate;
            }
        }

        return today.AddDays(1);
    }

    public static bool TryResolveDue(AlarmEntry alarm, DateTime nowLocal, DateTime nowUtc, out DateTime dueUtc,
        out long key)
    {
        return alarm.Eorzea
            ? TryResolveEorzea(alarm, nowUtc, out dueUtc, out key)
            : TryResolveLocal(alarm, nowLocal, nowUtc, out dueUtc, out key);
    }

    public static void Arm(AlarmEntry alarm, DateTime nowLocal, DateTime nowUtc)
    {
        alarm.LastFiredEpochMinute = TryResolveDue(alarm, nowLocal, nowUtc, out _, out var key) ? key : 0;
    }

    private static bool TryResolveLocal(AlarmEntry alarm, DateTime nowLocal, DateTime nowUtc, out DateTime dueUtc,
        out long key)
    {
        dueUtc = default;
        key = 0;
        for (var dayOffset = 0; dayOffset <= 1; dayOffset++)
        {
            var candidate = nowLocal.Date.AddDays(-dayOffset).AddHours(alarm.Hour).AddMinutes(alarm.Minute);
            var elapsed = nowLocal - candidate;
            if (elapsed < TimeSpan.Zero || elapsed > CatchUpWindow)
            {
                continue;
            }

            if (alarm.Repeats && !alarm.RepeatsOn(candidate.DayOfWeek))
            {
                continue;
            }

            dueUtc = nowUtc - elapsed;
            key = MinuteKey(candidate);
            return true;
        }

        return false;
    }

    private static bool TryResolveEorzea(AlarmEntry alarm, DateTime nowUtc, out DateTime dueUtc, out long key)
    {
        var occurrence = EorzeaClock.LastOccurrence(alarm.MinuteOfDay, nowUtc);
        dueUtc = EorzeaClock.UtcAt(occurrence);
        key = EorzeaKey(occurrence);
        var elapsed = nowUtc - dueUtc;
        return elapsed >= TimeSpan.Zero && elapsed <= CatchUpWindow;
    }
}
