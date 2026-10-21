using Aetherphone.Core.News;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NewsMaintenanceTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 17, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FollowUpPostSetsTheWindowForTheWholeEvent()
    {
        var items = new[]
        {
            Item("follow", Start.AddHours(3).AddMinutes(10), Start, Start.AddHours(3).AddMinutes(25)),
            Item("original", Start.AddDays(-1), Start, Start.AddHours(4)),
        };
        var board = new MaintenanceBoard();
        board.Sync(items);
        var afterFollowUpEnd = Start.AddHours(3).AddMinutes(30).ToUnixTimeSeconds();

        Assert.Equal(MaintenanceStatus.Done, board.StatusOf(1, afterFollowUpEnd));
        Assert.Equal(Start.AddHours(3).AddMinutes(25).ToUnixTimeSeconds(), board.WindowOf(1).EndUnix);
        Assert.Equal(MaintenanceStatus.Done, board.Pick(afterFollowUpEnd).Status);
    }

    [Fact]
    public void ServicesSharingAStartKeepTheirOwnWindows()
    {
        var items = new[]
        {
            Titled("store", "Mog Station Maintenance (Sep. 17)", Start.AddHours(-2), Start, Start.AddHours(6)),
            Titled("follow", "All Worlds Maintenance (Sep. 17): Follow-up", Start.AddHours(-3), Start,
                Start.AddHours(3)),
            Titled("worlds", "All Worlds Maintenance (Sep. 17)", Start.AddDays(-1), Start, Start.AddHours(4)),
        };
        var board = new MaintenanceBoard();
        board.Sync(items);

        Assert.Equal(Start.AddHours(6).ToUnixTimeSeconds(), board.WindowOf(0).EndUnix);
        Assert.Equal(Start.AddHours(3).ToUnixTimeSeconds(), board.WindowOf(1).EndUnix);
        Assert.Equal(Start.AddHours(3).ToUnixTimeSeconds(), board.WindowOf(2).EndUnix);
    }

    [Fact]
    public void ActiveWindowWinsOverUpcoming()
    {
        var items = new[]
        {
            Item("next", Start.AddDays(-1), Start.AddDays(2), Start.AddDays(2).AddHours(1)),
            Item("now", Start.AddDays(-2), Start, Start.AddHours(4)),
        };
        var board = new MaintenanceBoard();
        board.Sync(items);
        var pick = board.Pick(Start.AddHours(1).ToUnixTimeSeconds());

        Assert.Equal(1, pick.Index);
        Assert.Equal(MaintenanceStatus.Active, pick.Status);
        Assert.Equal(0.25f, board.WindowOf(1).Progress(Start.AddHours(1).ToUnixTimeSeconds()), 3);
    }

    [Fact]
    public void SoonestUpcomingWindowIsPicked()
    {
        var items = new[]
        {
            Item("later", Start.AddDays(-1), Start.AddDays(5), Start.AddDays(5).AddHours(1)),
            Item("sooner", Start.AddDays(-2), Start.AddDays(1), Start.AddDays(1).AddHours(2)),
            Item("past", Start.AddDays(-9), Start.AddDays(-8), Start.AddDays(-8).AddHours(2)),
        };
        var board = new MaintenanceBoard();
        board.Sync(items);
        var pick = board.Pick(Start.ToUnixTimeSeconds());

        Assert.Equal(1, pick.Index);
        Assert.Equal(MaintenanceStatus.Upcoming, pick.Status);
    }

    [Fact]
    public void LatestFinishedWindowIsPickedWhenNothingIsAhead()
    {
        var items = new[]
        {
            Item("older", Start.AddDays(-9), Start.AddDays(-8), Start.AddDays(-8).AddHours(2)),
            Item("newer", Start.AddDays(-3), Start.AddDays(-2), Start.AddDays(-2).AddHours(2)),
        };
        var board = new MaintenanceBoard();
        board.Sync(items);
        var pick = board.Pick(Start.ToUnixTimeSeconds());

        Assert.Equal(1, pick.Index);
        Assert.Equal(MaintenanceStatus.Done, pick.Status);
    }

    [Fact]
    public void PostsWithoutAWindowNeverPick()
    {
        var items = new[] { new LodestoneNewsItem { Id = "notice", Time = Start } };
        var board = new MaintenanceBoard();
        board.Sync(items);

        Assert.False(board.Pick(Start.ToUnixTimeSeconds()).Exists);
        Assert.Equal(MaintenanceStatus.None, board.StatusOf(0, Start.ToUnixTimeSeconds()));
    }

    [Fact]
    public void SyncSkipsTheSameSnapshot()
    {
        var items = new[] { Item("only", Start, Start, Start.AddHours(1)) };
        var board = new MaintenanceBoard();

        Assert.True(board.Sync(items));
        Assert.False(board.Sync(items));
    }

    [Fact]
    public void ParagraphsSplitOnBlankLines()
    {
        var paragraphs = NewsFormat.Paragraphs("First line.\r\n\r\nSecond line\nstill second.\n\n\n");

        Assert.Equal(new[] { "First line.", "Second line\nstill second." }, paragraphs);
        Assert.Empty(NewsFormat.Paragraphs("   "));
        Assert.Empty(NewsFormat.Paragraphs(null));
    }

    [Fact]
    public void CategoryPathsRoundTrip()
    {
        for (var index = 0; index < NewsCategories.All.Length; index++)
        {
            var category = NewsCategories.All[index];
            Assert.True(NewsCategories.TryParse(NewsCategories.Path(category), out var parsed));
            Assert.Equal(category, parsed);
        }

        Assert.False(NewsCategories.TryParse("unknown", out _));
        Assert.False(NewsCategories.AvailableFor(NewsCategory.Status, "cn"));
        Assert.True(NewsCategories.AvailableFor(NewsCategory.Status, "na"));
    }

    private static LodestoneNewsItem Titled(string id, string title, DateTimeOffset published,
        DateTimeOffset start, DateTimeOffset end) =>
        new() { Id = id, Title = title, Time = published, Start = start, End = end };

    private static LodestoneNewsItem Item(string id, DateTimeOffset published, DateTimeOffset start,
        DateTimeOffset end) =>
        new() { Id = id, Time = published, Start = start, End = end };
}
