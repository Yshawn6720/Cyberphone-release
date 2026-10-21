using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChatLogTests
{
    private static ChatEntry Entry(ChatLog log, string channelKey, string name, string world, string text,
        DateTime at, ChatEntryFlags flags = ChatEntryFlags.None) =>
        new(log.NextSequence(), channelKey, name, world, text, new[] { ChatChunk.Plain(text) }, at, flags);

    [Fact]
    public void ChannelLinesShareOneStream()
    {
        var log = new ChatLog();
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.FreeCompanyKey, "Rin", "Siren", "first", now));
        log.Append(Entry(log, GameChannels.FreeCompanyKey, "Mira", "Siren", "second", now.AddSeconds(1)));

        var lines = log.Lines(GameChannels.FreeCompanyKey);
        Assert.Equal(2, lines.Count);
        Assert.Equal("first", lines[0].Text);
        Assert.True(log.HasLines(GameChannels.FreeCompanyKey));
    }

    [Fact]
    public void TellsSplitPerCounterpartButShareTheChannel()
    {
        var log = new ChatLog();
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.TellKey, "Aria", "Siren", "hey", now));
        log.Append(Entry(log, GameChannels.TellKey, "Kael", "Siren", "yo", now.AddSeconds(1)));
        log.Append(Entry(log, GameChannels.TellKey, "Aria", "Siren", "again", now.AddSeconds(2)));

        Assert.Equal(2, log.Lines(ChatStreams.ForTell("Aria@Siren")).Count);
        Assert.Single(log.Lines(ChatStreams.ForTell("Kael@Siren")));
        Assert.Empty(log.Lines(GameChannels.TellKey));
    }

    [Fact]
    public void TellStreamKeyIgnoresCasing()
    {
        var log = new ChatLog();
        log.Append(Entry(log, GameChannels.TellKey, "Aria Solveig", "Siren", "hey", DateTime.Now));
        Assert.Single(log.Lines(ChatStreams.ForTell("aria solveig@siren")));
    }

    [Fact]
    public void StreamKeysRoundTripThroughChatStreams()
    {
        var stream = ChatStreams.ForTell("Aria@Siren");
        Assert.True(ChatStreams.IsTell(stream));
        Assert.Equal("aria@siren", ChatStreams.TellTarget(stream));

        var channel = ChatStreams.ForChannel(GameChannels.PartyKey);
        Assert.False(ChatStreams.IsTell(channel));
        Assert.Equal(string.Empty, ChatStreams.TellTarget(channel));
    }

    [Fact]
    public void RestoreOrdersHistoryBeforeLiveLines()
    {
        var log = new ChatLog();
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.PartyKey, "Nala", "Siren", "live", now));

        var history = new List<ChatEntry>
        {
            Entry(log, GameChannels.PartyKey, "Nala", "Siren", "older", now.AddMinutes(-10)),
            Entry(log, GameChannels.PartyKey, "Nala", "Siren", "oldest", now.AddMinutes(-20)),
        };
        log.Restore(GameChannels.PartyKey, history);

        var lines = log.Lines(GameChannels.PartyKey);
        Assert.Equal(3, lines.Count);
        Assert.Equal("oldest", lines[0].Text);
        Assert.Equal("older", lines[1].Text);
        Assert.Equal("live", lines[2].Text);
    }

    [Fact]
    public void SequencesAreUniqueAcrossStreams()
    {
        var log = new ChatLog();
        var seen = new HashSet<long>();
        var now = DateTime.Now;
        for (var index = 0; index < 50; index++)
        {
            var entry = Entry(log, index % 2 == 0 ? GameChannels.SayKey : GameChannels.PartyKey, "Rin", "Siren",
                "line", now.AddSeconds(index));
            log.Append(entry);
            Assert.True(seen.Add(entry.Sequence));
            Assert.Equal(entry.Sequence.ToString(), entry.Id);
        }
    }

    [Fact]
    public void BuffersStayBoundedAndKeepTheNewestLines()
    {
        var log = new ChatLog();
        var now = DateTime.Now;
        var total = ChatLog.MaxLinesPerStream + 500;
        for (var index = 0; index < total; index++)
        {
            log.Append(Entry(log, GameChannels.SayKey, "Rin", "Siren", $"line {index}", now.AddSeconds(index)));
        }

        var lines = log.Lines(GameChannels.SayKey);
        Assert.True(lines.Count <= ChatLog.MaxLinesPerStream + 128);
        Assert.Equal($"line {total - 1}", lines[lines.Count - 1].Text);
    }

    [Fact]
    public void ClearingOneStreamLeavesTheOthers()
    {
        var log = new ChatLog();
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.SayKey, "Rin", "Siren", "say", now));
        log.Append(Entry(log, GameChannels.PartyKey, "Nala", "Siren", "party", now));

        log.Clear(GameChannels.SayKey);
        Assert.False(log.HasLines(GameChannels.SayKey));
        Assert.True(log.HasLines(GameChannels.PartyKey));

        log.Clear();
        Assert.False(log.HasLines(GameChannels.PartyKey));
    }

    [Fact]
    public void CollectStreamsListsOnlyStreamsWithLines()
    {
        var log = new ChatLog();
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.FreeCompanyKey, "Rin", "Siren", "fc", now));
        log.Append(Entry(log, GameChannels.TellKey, "Aria", "Siren", "tell", now));

        var streams = new List<string>();
        log.CollectStreams(streams);
        Assert.Equal(2, streams.Count);
        Assert.Contains(GameChannels.FreeCompanyKey, streams);
        Assert.Contains(ChatStreams.ForTell("Aria@Siren"), streams);
    }

    [Fact]
    public void FlagsExposeSelfAndMention()
    {
        var log = new ChatLog();
        var mine = Entry(log, GameChannels.SayKey, "Me", "Siren", "hi", DateTime.Now, ChatEntryFlags.Self);
        var called = Entry(log, GameChannels.SayKey, "Rin", "Siren", "hi Me", DateTime.Now, ChatEntryFlags.Mention);

        Assert.True(mine.IsSelf);
        Assert.False(mine.IsMention);
        Assert.True(called.IsMention);
        Assert.False(called.IsSelf);
        Assert.Equal("Rin@Siren", called.SenderKey);
    }
}
