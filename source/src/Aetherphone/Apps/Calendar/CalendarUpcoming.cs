using System.Collections.Frozen;

namespace Aetherphone.Apps.Calendar;

internal readonly struct UpcomingItem
{
    public readonly ParsedEvent Event;
    public readonly DateTime Day;
    public readonly bool Ongoing;

    public UpcomingItem(ParsedEvent entry, DateTime day, bool ongoing)
    {
        Event = entry;
        Day = day;
        Ongoing = ongoing;
    }
}

internal static class CalendarUpcoming
{
    public const int Days = 30;

    public static void Collect(FrozenDictionary<long, ParsedEvent[]> merged, DateTime now, int days,
        List<UpcomingItem> output)
    {
        output.Clear();
        var today = now.Date;
        if (merged.TryGetValue(today.Ticks, out var todayEvents))
        {
            for (var index = 0; index < todayEvents.Length; index++)
            {
                var entry = todayEvents[index];
                if (entry.SpansDays && entry.Begin < now && entry.End > now)
                {
                    output.Add(new UpcomingItem(entry, today, true));
                }
            }
        }

        for (var dayIndex = 0; dayIndex < days; dayIndex++)
        {
            var day = today.AddDays(dayIndex);
            if (!merged.TryGetValue(day.Ticks, out var dayEvents))
            {
                continue;
            }

            for (var index = 0; index < dayEvents.Length; index++)
            {
                var entry = dayEvents[index];
                if (entry.Begin.Date != day)
                {
                    continue;
                }

                if (entry.SpansDays && entry.Begin < now && entry.End > now)
                {
                    continue;
                }

                var finished = entry.HasEnd ? entry.End <= now : entry.Begin < now;
                if (finished)
                {
                    continue;
                }

                output.Add(new UpcomingItem(entry, day, false));
            }
        }
    }
}
