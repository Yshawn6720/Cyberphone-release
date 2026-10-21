namespace Aetherphone.Core.Calendar;

internal static class CalendarRecurrence
{
    public const int NoOccurrence = -1;
    private const int DaysPerWeek = 7;
    private const int MonthsPerYear = 12;

    public static DateTime Occurrence(DateTime anchor, CalendarRepeat repeat, int index)
    {
        if (index <= 0)
        {
            return anchor;
        }

        return repeat switch
        {
            CalendarRepeat.Daily => anchor.AddDays(index),
            CalendarRepeat.Weekly => anchor.AddDays(index * DaysPerWeek),
            CalendarRepeat.Biweekly => anchor.AddDays(index * DaysPerWeek * 2),
            CalendarRepeat.Monthly => anchor.AddMonths(index),
            _ => anchor,
        };
    }

    public static int IndexAtOrBefore(DateTime anchor, CalendarRepeat repeat, DateTime moment)
    {
        if (moment < anchor)
        {
            return NoOccurrence;
        }

        if (repeat == CalendarRepeat.None)
        {
            return 0;
        }

        if (repeat == CalendarRepeat.Monthly)
        {
            var months = (moment.Year - anchor.Year) * MonthsPerYear + moment.Month - anchor.Month;
            while (months > 0 && Occurrence(anchor, repeat, months) > moment)
            {
                months--;
            }

            return months;
        }

        var stepTicks = TimeSpan.TicksPerDay * StepDays(repeat);
        return (int)Math.Min(int.MaxValue, (moment - anchor).Ticks / stepTicks);
    }

    public static int FirstIndexOnOrAfter(DateTime anchor, CalendarRepeat repeat, DateTime moment)
    {
        if (moment <= anchor)
        {
            return 0;
        }

        if (repeat == CalendarRepeat.None)
        {
            return NoOccurrence;
        }

        var index = IndexAtOrBefore(anchor, repeat, moment);
        return Occurrence(anchor, repeat, index) < moment ? index + 1 : index;
    }

    private static int StepDays(CalendarRepeat repeat) => repeat switch
    {
        CalendarRepeat.Weekly => DaysPerWeek,
        CalendarRepeat.Biweekly => DaysPerWeek * 2,
        _ => 1,
    };
}
