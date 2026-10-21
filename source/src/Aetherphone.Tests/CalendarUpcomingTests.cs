using System.Collections.Frozen;
using Aetherphone.Apps.Calendar;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CalendarUpcomingTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0);

    [Fact]
    public void RunningMultiDayEventsListOnceUnderHappeningNow()
    {
        var trove = Event("Treasure Trove", Now.AddDays(-5), Now.AddDays(10));
        var merged = Map((Now.Date, trove), (Now.Date.AddDays(1), trove), (Now.Date.AddDays(2), trove));
        var items = new List<UpcomingItem>();

        CalendarUpcoming.Collect(merged, Now, CalendarUpcoming.Days, items);

        var item = Assert.Single(items);
        Assert.True(item.Ongoing);
        Assert.Equal("Treasure Trove", item.Event.Name);
    }

    [Fact]
    public void FinishedEventsDropAndLaterOnesKeepTheirDay()
    {
        var morning = Event("Morning", Now.AddHours(-3), Now.AddHours(-2));
        var evening = Event("Evening", Now.AddHours(8), Now.AddHours(9));
        var tomorrow = Event("Tomorrow", Now.AddDays(1), Now.AddDays(1));
        var merged = Map((Now.Date, morning), (Now.Date, evening), (Now.Date.AddDays(1), tomorrow));
        var items = new List<UpcomingItem>();

        CalendarUpcoming.Collect(merged, Now, CalendarUpcoming.Days, items);

        Assert.Equal(2, items.Count);
        Assert.Equal("Evening", items[0].Event.Name);
        Assert.Equal(Now.Date, items[0].Day);
        Assert.Equal(Now.Date.AddDays(1), items[1].Day);
        Assert.False(items[1].Ongoing);
    }

    [Fact]
    public void FutureMultiDayEventsShowOnlyOnTheirFirstDay()
    {
        var festival = Event("Festival", Now.AddDays(2), Now.AddDays(6));
        var merged = Map((Now.Date.AddDays(2), festival), (Now.Date.AddDays(3), festival));
        var items = new List<UpcomingItem>();

        CalendarUpcoming.Collect(merged, Now, CalendarUpcoming.Days, items);

        var item = Assert.Single(items);
        Assert.Equal(Now.Date.AddDays(2), item.Day);
    }

    private static ParsedEvent Event(string name, DateTime begin, DateTime end) =>
        new() { Name = name, Begin = begin, End = end, Url = string.Empty, GroupName = string.Empty };

    private static FrozenDictionary<long, ParsedEvent[]> Map(params (DateTime Day, ParsedEvent Event)[] entries)
    {
        var builder = new Dictionary<long, List<ParsedEvent>>();
        foreach (var (day, entry) in entries)
        {
            if (!builder.TryGetValue(day.Ticks, out var list))
            {
                list = new List<ParsedEvent>();
                builder[day.Ticks] = list;
            }

            list.Add(entry);
        }

        var result = new Dictionary<long, ParsedEvent[]>();
        foreach (var pair in builder)
        {
            result[pair.Key] = pair.Value.ToArray();
        }

        return result.ToFrozenDictionary();
    }
}
