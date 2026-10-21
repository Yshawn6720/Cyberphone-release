using Aetherphone.Core.Notes;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ReminderBoardTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0);

    private static ReminderItem Reminder(string title, DateTime? due = null, bool done = false) =>
        new() { Title = title, DueAt = due, Done = done };

    [Fact]
    public void ClassifySplitsByDueDateAndCompletion()
    {
        Assert.Equal(ReminderGroup.Overdue, ReminderBoard.Classify(Reminder("a", Now.AddMinutes(-1)), Now));
        Assert.Equal(ReminderGroup.Today, ReminderBoard.Classify(Reminder("b", Now.AddHours(3)), Now));
        Assert.Equal(ReminderGroup.Scheduled, ReminderBoard.Classify(Reminder("c", Now.AddDays(1)), Now));
        Assert.Equal(ReminderGroup.Anytime, ReminderBoard.Classify(Reminder("d"), Now));
        Assert.Equal(ReminderGroup.Completed, ReminderBoard.Classify(Reminder("e", Now.AddDays(-3), true), Now));
    }

    [Fact]
    public void AllFilterOrdersGroupsAndSortsDatedRemindersByDue()
    {
        var reminders = new List<ReminderItem>
        {
            Reminder("done", done: true),
            Reminder("later", Now.AddDays(3)),
            Reminder("anytime"),
            Reminder("sooner", Now.AddDays(1)),
            Reminder("late", Now.AddHours(-2)),
        };
        var board = new ReminderBoard();

        board.Build(reminders, Now, ReminderFilter.All, Guid.Empty);

        Assert.Equal(new[] { 4, 3, 1, 2, 0 }, board.Order);
        Assert.Equal(4, board.Sections.Count);
        Assert.Equal(new ReminderSection(ReminderGroup.Scheduled, 1, 2), board.Sections[1]);
        Assert.Equal(4, board.OpenCount);
        Assert.Equal(1, board.CountOf(ReminderGroup.Completed));
    }

    [Fact]
    public void FiltersKeepOnlyTheirGroupsButCountEverything()
    {
        var reminders = new List<ReminderItem>
        {
            Reminder("late", Now.AddHours(-2)),
            Reminder("today", Now.AddHours(2)),
            Reminder("tomorrow", Now.AddDays(1)),
            Reminder("anytime"),
            Reminder("done", done: true),
        };
        var board = new ReminderBoard();

        board.Build(reminders, Now, ReminderFilter.Today, Guid.Empty);
        Assert.Equal(new[] { 0, 1 }, board.Order);

        board.Build(reminders, Now, ReminderFilter.Scheduled, Guid.Empty);
        Assert.Equal(new[] { 0, 1, 2 }, board.Order);

        board.Build(reminders, Now, ReminderFilter.Completed, Guid.Empty);
        Assert.Equal(new[] { 4 }, board.Order);
        Assert.Equal(1, board.CountOf(ReminderGroup.Anytime));
    }

    [Fact]
    public void AJustCompletedReminderLingersInItsOldSection()
    {
        var finished = Reminder("finished", Now.AddHours(1), true);
        var reminders = new List<ReminderItem> { Reminder("open", Now.AddHours(2)), finished };
        var board = new ReminderBoard();

        board.Build(reminders, Now, ReminderFilter.All, finished.Id);

        Assert.Single(board.Sections);
        Assert.Equal(ReminderGroup.Today, board.Sections[0].Group);
        Assert.Equal(new[] { 1, 0 }, board.Order);
    }

    [Theory]
    [InlineData((int)ReminderQuickDate.Today, 7)]
    [InlineData((int)ReminderQuickDate.Tomorrow, 8)]
    [InlineData((int)ReminderQuickDate.Weekend, 10)]
    [InlineData((int)ReminderQuickDate.NextWeek, 12)]
    public void QuickDatesLandOnTheExpectedDay(int kind, int expectedDay)
    {
        Assert.Equal(new DateTime(2026, 10, expectedDay), ReminderBoard.QuickDate((ReminderQuickDate)kind, Now));
    }

    [Fact]
    public void WeekendOnSaturdayIsTodayAndNextWeekOnMondayIsAWeekOut()
    {
        var saturday = new DateTime(2026, 10, 10, 9, 0, 0);
        var monday = new DateTime(2026, 10, 12, 9, 0, 0);

        Assert.Equal(saturday.Date, ReminderBoard.QuickDate(ReminderQuickDate.Weekend, saturday));
        Assert.Equal(monday.Date.AddDays(7), ReminderBoard.QuickDate(ReminderQuickDate.NextWeek, monday));
    }

    [Fact]
    public void DefaultTimeIsTheNextFullHourButStaysInTheDay()
    {
        Assert.Equal(13 * 60, ReminderBoard.DefaultMinuteOfDay(Now.AddMinutes(20)));
        Assert.Equal(23 * 60 + 55, ReminderBoard.DefaultMinuteOfDay(new DateTime(2026, 10, 7, 23, 40, 0)));
    }
}
