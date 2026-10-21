using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChatSearchTests
{
    private static ChatEntry Entry(ChatLog log, string channelKey, string name, string text, DateTime at) =>
        new(log.NextSequence(), channelKey, name, "Siren", text, new[] { ChatChunk.Plain(text) }, at,
            ChatEntryFlags.None);

    private static ChatTab Tab(string name, params string[] channels) => new()
    {
        Id = name,
        Name = name,
        Channels = new List<string>(channels),
        Alerts = AlertPolicy.All,
    };

    private static (ChatLog Log, ChatInbox Inbox, ChatSearch Search) Build(params ChatTab[] tabs)
    {
        var configuration = new Configuration();
        configuration.LinkpearlTabs.AddRange(tabs);
        var log = new ChatLog();
        var store = new TabStore(configuration, new Aetherphone.Core.Game.CharacterWatch(null!));
        return (log, new ChatInbox(log, store, new TellPreferences(configuration), configuration), new ChatSearch());
    }

    [Fact]
    public void ShortQueriesDoNotSearch()
    {
        var (log, inbox, search) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        log.Append(Entry(log, "fc", "Rin", "maps tonight", DateTime.Now));
        inbox.Sync();

        search.Run("m", inbox, log);
        Assert.False(search.Active);
        Assert.Empty(search.Hits);
    }

    [Fact]
    public void FindsMessagesByTextAcrossTabsAndTells()
    {
        var (log, inbox, search) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        var now = DateTime.Now;
        log.Append(Entry(log, "fc", "Rin", "anyone up for maps?", now));
        log.Append(Entry(log, GameChannels.TellKey, "Aria", "bring your maps", now.AddSeconds(1)));
        log.Append(Entry(log, "fc", "Mira", "pulling now", now.AddSeconds(2)));
        inbox.Sync();

        search.Run("maps", inbox, log);
        Assert.True(search.Active);
        Assert.Equal(2, search.Hits.Count);
        Assert.Equal("bring your maps", search.Hits[0].Entry.Text);
    }

    [Fact]
    public void MatchesSenderNamesToo()
    {
        var (log, inbox, search) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        log.Append(Entry(log, "fc", "Kael Ashford", "hello", DateTime.Now));
        inbox.Sync();

        search.Run("ashford", inbox, log);
        Assert.Single(search.Hits);
    }

    [Fact]
    public void HitsCarryTheConversationTheyBelongTo()
    {
        var (log, inbox, search) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        log.Append(Entry(log, "fc", "Rin", "raid tonight", DateTime.Now));
        log.Append(Entry(log, GameChannels.TellKey, "Aria", "raid ready", DateTime.Now.AddSeconds(1)));
        inbox.Sync();

        search.Run("raid", inbox, log);
        Assert.Equal(2, search.Hits.Count);
        Assert.Contains(search.Hits, hit => hit.ConversationKey == "tab:FC" && hit.Title == "FC");
        Assert.Contains(search.Hits, hit => ChatStreams.IsTell(hit.ConversationKey) && hit.Title == "Aria");
    }

    [Fact]
    public void NewestHitsComeFirst()
    {
        var (log, inbox, search) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        var now = DateTime.Now;
        log.Append(Entry(log, "fc", "Rin", "ping one", now.AddMinutes(-10)));
        log.Append(Entry(log, "fc", "Rin", "ping two", now));
        inbox.Sync();

        search.Run("ping", inbox, log);
        Assert.Equal("ping two", search.Hits[0].Entry.Text);
        Assert.Equal("ping one", search.Hits[1].Entry.Text);
    }

    [Fact]
    public void ResultsAreCappedAndSearchIsCaseInsensitive()
    {
        var (log, inbox, search) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        var now = DateTime.Now;
        for (var index = 0; index < ChatSearch.MaxHits + 40; index++)
        {
            log.Append(Entry(log, "fc", "Rin", $"NEEDLE {index}", now.AddSeconds(index)));
        }

        inbox.Sync();
        search.Run("needle", inbox, log);
        Assert.Equal(ChatSearch.MaxHits, search.Hits.Count);
    }

    [Fact]
    public void ClearingStopsTheSearch()
    {
        var (log, inbox, search) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        log.Append(Entry(log, "fc", "Rin", "maps", DateTime.Now));
        inbox.Sync();
        search.Run("maps", inbox, log);
        Assert.NotEmpty(search.Hits);

        search.Clear();
        Assert.False(search.Active);
        Assert.Empty(search.Hits);
    }
}
