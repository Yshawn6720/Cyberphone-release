using Aetherphone.Core.Calendar;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CalendarRecurrenceTests
{
    private static readonly DateTime Anchor = new(2026, 1, 31, 20, 0, 0);

    [Fact]
    public void WeeklyOccurrencesStepBySevenDays()
    {
        Assert.Equal(Anchor.AddDays(14), CalendarRecurrence.Occurrence(Anchor, CalendarRepeat.Weekly, 2));
        Assert.Equal(Anchor.AddDays(28), CalendarRecurrence.Occurrence(Anchor, CalendarRepeat.Biweekly, 2));
    }

    [Fact]
    public void MonthlyOccurrencesClampToShortMonthsWithoutDrifting()
    {
        Assert.Equal(new DateTime(2026, 2, 28, 20, 0, 0),
            CalendarRecurrence.Occurrence(Anchor, CalendarRepeat.Monthly, 1));
        Assert.Equal(new DateTime(2026, 3, 31, 20, 0, 0),
            CalendarRecurrence.Occurrence(Anchor, CalendarRepeat.Monthly, 2));
    }

    [Fact]
    public void IndexAtOrBeforeFindsTheLatestStartedOccurrence()
    {
        var moment = Anchor.AddDays(15);

        Assert.Equal(2, CalendarRecurrence.IndexAtOrBefore(Anchor, CalendarRepeat.Weekly, moment));
        Assert.Equal(CalendarRecurrence.NoOccurrence,
            CalendarRecurrence.IndexAtOrBefore(Anchor, CalendarRepeat.Weekly, Anchor.AddMinutes(-1)));
    }

    [Fact]
    public void MonthlyIndexStepsBackWhenTheMonthsOccurrenceIsStillAhead()
    {
        var moment = new DateTime(2026, 3, 31, 19, 0, 0);

        Assert.Equal(1, CalendarRecurrence.IndexAtOrBefore(Anchor, CalendarRepeat.Monthly, moment));
    }

    [Fact]
    public void FirstIndexOnOrAfterRoundsUpBetweenOccurrences()
    {
        Assert.Equal(0, CalendarRecurrence.FirstIndexOnOrAfter(Anchor, CalendarRepeat.Daily, Anchor.AddDays(-3)));
        Assert.Equal(3, CalendarRecurrence.FirstIndexOnOrAfter(Anchor, CalendarRepeat.Daily, Anchor.AddDays(2.5)));
        Assert.Equal(2, CalendarRecurrence.FirstIndexOnOrAfter(Anchor, CalendarRepeat.Daily, Anchor.AddDays(2)));
        Assert.Equal(CalendarRecurrence.NoOccurrence,
            CalendarRecurrence.FirstIndexOnOrAfter(Anchor, CalendarRepeat.None, Anchor.AddDays(1)));
    }
}
