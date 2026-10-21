using System.Globalization;

namespace Aetherphone.Core.GameChat;

[Flags]
internal enum ChatEntryFlags : byte
{
    None = 0,
    Self = 1,
    Mention = 2,
}

internal sealed class ChatEntry
{
    public ChatEntry(long sequence, string channelKey, string authorName, string authorWorld, string text,
        ChatChunk[] chunks, DateTime at, ChatEntryFlags flags)
    {
        Sequence = sequence;
        Id = sequence.ToString(CultureInfo.InvariantCulture);
        ChannelKey = channelKey;
        AuthorName = authorName;
        AuthorWorld = authorWorld;
        Text = text;
        Chunks = chunks;
        At = at;
        Flags = flags;
        StreamKey = string.Equals(channelKey, GameChannels.TellKey, StringComparison.Ordinal)
            ? ChatStreams.ForTell(SenderKey)
            : ChatStreams.ForChannel(channelKey);
    }

    public string StreamKey { get; }

    public long Sequence { get; }

    public string Id { get; }

    public string ChannelKey { get; }

    public string AuthorName { get; }

    public string AuthorWorld { get; }

    public string Text { get; }

    public ChatChunk[] Chunks { get; }

    public DateTime At { get; }

    public ChatEntryFlags Flags { get; }

    public bool IsSelf => (Flags & ChatEntryFlags.Self) != 0;

    public bool IsMention => (Flags & ChatEntryFlags.Mention) != 0;

    public string SenderKey => AuthorWorld.Length > 0 ? string.Concat(AuthorName, "@", AuthorWorld) : AuthorName;
}
