using System.Collections.Frozen;
using System.Globalization;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Calendar;

internal struct AgendaRow
{
    public ParsedEvent Event;
    public string Subtitle;
}

internal readonly struct AgendaSection
{
    public readonly string Title;
    public readonly int Start;
    public readonly int Count;

    public AgendaSection(string title, int start, int count)
    {
        Title = title;
        Start = start;
        Count = count;
    }
}

internal sealed class CalendarAgenda
{
    private const string Separator = " · ";
    private const string RangeSeparator = " – ";

    private readonly List<AgendaRow> dayRows = new();
    private readonly List<AgendaRow> upcomingRows = new();
    private readonly List<AgendaSection> sections = new();
    private readonly List<UpcomingItem> items = new();
    private FrozenDictionary<long, ParsedEvent[]>? dayMerged;
    private long dayKey = -1;
    private long dayMinute = -1;
    private CultureInfo? dayCulture;
    private int dayFormat = -1;
    private FrozenDictionary<long, ParsedEvent[]>? upcomingMerged;
    private long upcomingMinute = -1;
    private CultureInfo? upcomingCulture;
    private int upcomingFormat = -1;

    public List<AgendaRow> DayRows => dayRows;

    public List<AgendaRow> UpcomingRows => upcomingRows;

    public List<AgendaSection> Sections => sections;

    public void Invalidate()
    {
        dayMerged = null;
        upcomingMerged = null;
    }

    public void SyncDay(FrozenDictionary<long, ParsedEvent[]> merged, DateTime day)
    {
        var minute = DateTime.Now.Ticks / TimeSpan.TicksPerMinute;
        if (ReferenceEquals(dayMerged, merged) && dayKey == day.Ticks && dayMinute == minute &&
            ReferenceEquals(dayCulture, Loc.Culture) && dayFormat == TimeText.FormatVersion)
        {
            return;
        }

        dayMerged = merged;
        dayKey = day.Ticks;
        dayMinute = minute;
        dayCulture = Loc.Culture;
        dayFormat = TimeText.FormatVersion;
        dayRows.Clear();
        if (!merged.TryGetValue(day.Ticks, out var dayEvents))
        {
            return;
        }

        for (var index = 0; index < dayEvents.Length; index++)
        {
            var entry = dayEvents[index];
            dayRows.Add(new AgendaRow { Event = entry, Subtitle = WithGroup(TimeOnDay(entry, day), entry) });
        }
    }

    public void SyncUpcoming(FrozenDictionary<long, ParsedEvent[]> merged)
    {
        var now = DateTime.Now;
        var minute = now.Ticks / TimeSpan.TicksPerMinute;
        if (ReferenceEquals(upcomingMerged, merged) && upcomingMinute == minute &&
            ReferenceEquals(upcomingCulture, Loc.Culture) && upcomingFormat == TimeText.FormatVersion)
        {
            return;
        }

        upcomingMerged = merged;
        upcomingMinute = minute;
        upcomingCulture = Loc.Culture;
        upcomingFormat = TimeText.FormatVersion;
        upcomingRows.Clear();
        sections.Clear();
        CalendarUpcoming.Collect(merged, now, CalendarUpcoming.Days, items);
        var sectionStart = 0;
        var sectionDay = DateTime.MinValue;
        var sectionOngoing = false;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var startsSection = index == 0 || item.Ongoing != sectionOngoing || item.Day != sectionDay;
            if (startsSection && index > 0)
            {
                sections.Add(new AgendaSection(SectionTitle(sectionOngoing, sectionDay, now.Date), sectionStart,
                    upcomingRows.Count - sectionStart));
            }

            if (startsSection)
            {
                sectionStart = upcomingRows.Count;
                sectionDay = item.Day;
                sectionOngoing = item.Ongoing;
            }

            var subtitle = item.Ongoing
                ? Loc.T(L.Calendar.EndsIn, TimeText.Until(item.Event.End - now))
                : TimeOnDay(item.Event, item.Day);
            upcomingRows.Add(new AgendaRow { Event = item.Event, Subtitle = WithGroup(subtitle, item.Event) });
        }

        if (items.Count > 0)
        {
            sections.Add(new AgendaSection(SectionTitle(sectionOngoing, sectionDay, now.Date), sectionStart,
                upcomingRows.Count - sectionStart));
        }
    }

    public static string DayTitle(DateTime day, DateTime today)
    {
        if (day == today)
        {
            return Loc.T(L.Calendar.Today);
        }

        if (day == today.AddDays(1))
        {
            return Loc.T(L.Calendar.Tomorrow);
        }

        return LongDay(day);
    }

    public static string LongDay(DateTime day) =>
        Loc.Culture.TextInfo.ToTitleCase(day.ToString("dddd, MMMM d", Loc.Culture));

    public static string TimeOnDay(in ParsedEvent entry, DateTime day)
    {
        if (entry.IsCustom)
        {
            return entry.HasEnd
                ? string.Concat(TimeText.Clock(entry.Begin), RangeSeparator, TimeText.Clock(entry.End))
                : TimeText.Clock(entry.Begin);
        }

        if (!entry.SpansDays)
        {
            return entry.HasEnd
                ? string.Concat(TimeText.Clock(entry.Begin), RangeSeparator, TimeText.Clock(entry.End))
                : TimeText.Clock(entry.Begin);
        }

        if (entry.Begin.Date == day)
        {
            return Loc.T(L.Calendar.StartsAt, TimeText.Clock(entry.Begin));
        }

        if (entry.End.Date == day)
        {
            return Loc.T(L.Calendar.EndsAt, TimeText.Clock(entry.End));
        }

        return Loc.T(L.Calendar.AllDay);
    }

    public static string Range(in ParsedEvent entry)
    {
        if (!entry.HasEnd)
        {
            return TimeText.Clock(entry.Begin);
        }

        if (!entry.SpansDays)
        {
            return string.Concat(TimeText.Clock(entry.Begin), RangeSeparator, TimeText.Clock(entry.End));
        }

        return string.Concat(ShortMoment(entry.Begin), RangeSeparator, ShortMoment(entry.End));
    }

    public static string ShortMoment(DateTime moment) =>
        string.Concat(moment.ToString(Loc.Culture.DateTimeFormat.MonthDayPattern, Loc.Culture), ", ",
            TimeText.Clock(moment));

    private static string WithGroup(string subtitle, in ParsedEvent entry) =>
        string.IsNullOrEmpty(entry.GroupName) ? subtitle : string.Concat(subtitle, Separator, entry.GroupName);

    private static string SectionTitle(bool ongoing, DateTime day, DateTime today) =>
        ongoing ? Loc.T(L.Calendar.HappeningNow) : DayTitle(day, today);
}
