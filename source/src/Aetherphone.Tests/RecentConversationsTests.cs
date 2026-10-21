using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RecentConversationsTests
{
    private static readonly DateTime Origin = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void PinnedAndLooseRowsInterleaveByActivity()
    {
        var pinned = new List<InboxRow> { Row("tab:fc", 5), Row("tab:ls", 1) };
        var rows = new List<InboxRow> { Row("tell:alisaie", 9), Row("tell:yshtola", 3) };
        var destination = new InboxRow[8];

        var count = RecentConversations.Collect(pinned, rows, destination);

        Assert.Equal(4, count);
        Assert.Equal("tell:alisaie", destination[0].Key);
        Assert.Equal("tab:fc", destination[1].Key);
        Assert.Equal("tell:yshtola", destination[2].Key);
        Assert.Equal("tab:ls", destination[3].Key);
    }

    [Fact]
    public void TheBufferKeepsOnlyTheMostRecentRows()
    {
        var rows = new List<InboxRow> { Row("a", 1), Row("b", 7), Row("c", 4), Row("d", 2) };
        var destination = new InboxRow[2];

        var count = RecentConversations.Collect(Array.Empty<InboxRow>(), rows, destination);

        Assert.Equal(2, count);
        Assert.Equal("b", destination[0].Key);
        Assert.Equal("c", destination[1].Key);
    }

    [Fact]
    public void APressOutsideTheWindowStartsBackAtTheTop()
    {
        Assert.Equal(0, RecentConversations.NextIndex(-1, 3, false));
        Assert.Equal(0, RecentConversations.NextIndex(2, 3, false));
    }

    [Fact]
    public void PressingAgainWalksDownAndWrapsAround()
    {
        Assert.Equal(1, RecentConversations.NextIndex(0, 3, true));
        Assert.Equal(2, RecentConversations.NextIndex(1, 3, true));
        Assert.Equal(0, RecentConversations.NextIndex(2, 3, true));
    }

    [Fact]
    public void AnEmptyInboxHasNothingToCycle()
    {
        Assert.Equal(-1, RecentConversations.NextIndex(-1, 0, true));
    }

    [Fact]
    public void AShrunkenListNeverPointsPastItsEnd()
    {
        Assert.Equal(0, RecentConversations.NextIndex(5, 2, true));
    }

    private static InboxRow Row(string key, int minutes) => new()
    {
        Key = key,
        Title = key,
        LastActivity = Origin.AddMinutes(minutes),
    };
}
