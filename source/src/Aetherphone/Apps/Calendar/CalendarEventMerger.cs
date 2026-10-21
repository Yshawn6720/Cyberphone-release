using System.Collections.Frozen;
using Aetherphone.Core.Calendar;

namespace Aetherphone.Apps.Calendar;

internal static class CalendarEventMerger
{
    private const float DimAlpha = 0.42f;
    private const int MaxOccurrences = 2000;
    private const int MaxSpanDays = 31;

    private static readonly Comparison<ParsedEvent> ByStart = static (left, right) =>
    {
        var begin = left.Begin.CompareTo(right.Begin);
        return begin != 0 ? begin : string.CompareOrdinal(left.Name, right.Name);
    };

    public static FrozenDictionary<long, ParsedEvent[]> Merge(FrozenDictionary<long, ParsedEvent[]> remote,
        IReadOnlyList<CalendarCustomEvent> custom, IReadOnlyList<CalendarEventGroup> groups, bool showGameEvents,
        CalendarSurface surface, Vector4 customColor, DateTime windowStart, DateTime windowEnd)
    {
        var visibleRemote = showGameEvents ? remote : FrozenDictionary<long, ParsedEvent[]>.Empty;
        if (custom.Count == 0)
        {
            return visibleRemote;
        }

        var builder = new Dictionary<long, List<ParsedEvent>>(visibleRemote.Count + custom.Count);
        foreach (var pair in visibleRemote)
        {
            builder[pair.Key] = new List<ParsedEvent>(pair.Value);
        }

        for (var index = 0; index < custom.Count; index++)
        {
            var item = custom[index];
            var group = FindGroup(groups, item.GroupId);
            if (group is not null && !group.ShowsOn(surface))
            {
                continue;
            }

            var color = CalendarColors.For(group, customColor);
            if (item.Repeat == CalendarRepeat.None)
            {
                AddOccurrence(builder, item, group, color, item.When);
                continue;
            }

            var span = TimeSpan.FromMinutes(Math.Max(0, item.DurationMinutes));
            var first = CalendarRecurrence.FirstIndexOnOrAfter(item.When, item.Repeat, windowStart - span);
            if (first == CalendarRecurrence.NoOccurrence)
            {
                continue;
            }

            for (var occurrenceIndex = first; occurrenceIndex < first + MaxOccurrences; occurrenceIndex++)
            {
                var occurrence = CalendarRecurrence.Occurrence(item.When, item.Repeat, occurrenceIndex);
                if (occurrence >= windowEnd)
                {
                    break;
                }

                AddOccurrence(builder, item, group, color, occurrence);
            }
        }

        var result = new Dictionary<long, ParsedEvent[]>(builder.Count);
        foreach (var pair in builder)
        {
            var array = pair.Value.ToArray();
            Array.Sort(array, ByStart);
            result[pair.Key] = array;
        }

        return result.ToFrozenDictionary();
    }

    private static void AddOccurrence(Dictionary<long, List<ParsedEvent>> builder, CalendarCustomEvent item,
        CalendarEventGroup? group, Vector4 color, DateTime occurrence)
    {
        var end = item.EndOf(occurrence);
        var parsed = new ParsedEvent
        {
            Name = item.Title,
            Begin = occurrence,
            End = end,
            Url = string.Empty,
            Color = color,
            DimColor = color with { W = DimAlpha },
            IsCustom = true,
            CustomId = item.Id,
            GroupName = group?.Name ?? string.Empty,
            Repeats = item.Repeat != CalendarRepeat.None,
        };

        var lastDay = end > occurrence ? end.AddTicks(-1).Date : occurrence.Date;
        var day = occurrence.Date;
        for (var dayIndex = 0; dayIndex < MaxSpanDays && day <= lastDay; dayIndex++)
        {
            var key = day.Ticks;
            if (!builder.TryGetValue(key, out var dayEvents))
            {
                dayEvents = new List<ParsedEvent>();
                builder[key] = dayEvents;
            }

            dayEvents.Add(parsed);
            day = day.AddDays(1);
        }
    }

    private static CalendarEventGroup? FindGroup(IReadOnlyList<CalendarEventGroup> groups, Guid groupId)
    {
        if (groupId == Guid.Empty)
        {
            return null;
        }

        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].Id == groupId)
            {
                return groups[index];
            }
        }

        return null;
    }
}
