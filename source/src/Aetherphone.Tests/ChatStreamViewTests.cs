using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChatStreamViewTests
{
    private static ChatEntry Entry(ChatLog log, string channelKey, string name, DateTime at) =>
        new(log.NextSequence(), channelKey, name, "Siren", $"{name} at {at:HH:mm:ss}",
            new[] { ChatChunk.Plain("line") }, at, ChatEntryFlags.None);

    [Fact]
    public void MergesSeveralChannelsInTimeOrder()
    {
        var log = new ChatLog();
        using var view = new ChatStreamView(log);
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.FreeCompanyKey, "Rin", now));
        log.Append(Entry(log, "ls1", "Kael", now.AddSeconds(1)));
        log.Append(Entry(log, GameChannels.FreeCompanyKey, "Mira", now.AddSeconds(2)));

        view.Target(new[] { GameChannels.FreeCompanyKey, "ls1" });
        view.Sync();

        Assert.Equal(3, view.Entries.Count);
        Assert.Equal("Rin", view.Entries[0].AuthorName);
        Assert.Equal("Kael", view.Entries[1].AuthorName);
        Assert.Equal("Mira", view.Entries[2].AuthorName);
    }

    [Fact]
    public void IgnoresChannelsOutsideTheTarget()
    {
        var log = new ChatLog();
        using var view = new ChatStreamView(log);
        view.Target(new[] { GameChannels.FreeCompanyKey });
        view.Sync();

        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.SayKey, "Stranger", now));
        view.Sync();
        Assert.Empty(view.Entries);

        log.Append(Entry(log, GameChannels.FreeCompanyKey, "Rin", now.AddSeconds(1)));
        view.Sync();
        Assert.Single(view.Entries);
    }

    [Fact]
    public void AppendsLiveLinesWithoutARebuild()
    {
        var log = new ChatLog();
        using var view = new ChatStreamView(log);
        view.Target(new[] { GameChannels.PartyKey });
        view.Sync();
        var revision = view.Revision;

        log.Append(Entry(log, GameChannels.PartyKey, "Nala", DateTime.Now));
        Assert.Single(view.Entries);
        Assert.True(view.Revision > revision);

        view.Sync();
        Assert.Single(view.Entries);
    }

    [Fact]
    public void RetargetingRebuildsFromTheLog()
    {
        var log = new ChatLog();
        using var view = new ChatStreamView(log);
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.FreeCompanyKey, "Rin", now));
        log.Append(Entry(log, "ls1", "Kael", now.AddSeconds(1)));

        view.Target(new[] { GameChannels.FreeCompanyKey });
        view.Sync();
        Assert.Single(view.Entries);

        view.Target(new[] { "ls1" });
        view.Sync();
        Assert.Single(view.Entries);
        Assert.Equal("Kael", view.Entries[0].AuthorName);
    }

    [Fact]
    public void RestoredHistoryTriggersARebuild()
    {
        var log = new ChatLog();
        using var view = new ChatStreamView(log);
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.FreeCompanyKey, "Rin", now));
        view.Target(new[] { GameChannels.FreeCompanyKey });
        view.Sync();
        Assert.Single(view.Entries);

        log.Restore(GameChannels.FreeCompanyKey, new List<ChatEntry>
        {
            Entry(log, GameChannels.FreeCompanyKey, "Older", now.AddMinutes(-5)),
        });
        view.Sync();

        Assert.Equal(2, view.Entries.Count);
        Assert.Equal("Older", view.Entries[0].AuthorName);
    }

    [Fact]
    public void TellStreamsStaySeparate()
    {
        var log = new ChatLog();
        using var view = new ChatStreamView(log);
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.TellKey, "Aria", now));
        log.Append(Entry(log, GameChannels.TellKey, "Kael", now.AddSeconds(1)));

        view.Target(new[] { ChatStreams.ForTell("Aria@Siren") });
        view.Sync();

        Assert.Single(view.Entries);
        Assert.Equal("Aria", view.Entries[0].AuthorName);
    }

    [Fact]
    public void DisposeStopsTracking()
    {
        var log = new ChatLog();
        var view = new ChatStreamView(log);
        view.Target(new[] { GameChannels.PartyKey });
        view.Sync();
        view.Dispose();

        log.Append(Entry(log, GameChannels.PartyKey, "Nala", DateTime.Now));
        Assert.Empty(view.Entries);
    }
}
