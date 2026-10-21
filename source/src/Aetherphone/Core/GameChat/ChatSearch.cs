namespace Aetherphone.Core.GameChat;

internal readonly struct ChatHit
{
    public readonly string ConversationKey;
    public readonly string Title;
    public readonly ChatEntry Entry;

    public ChatHit(string conversationKey, string title, ChatEntry entry)
    {
        ConversationKey = conversationKey;
        Title = title;
        Entry = entry;
    }
}

internal sealed class ChatSearch
{
    public const int MaxHits = 60;
    public const int MinQueryLength = 2;

    private readonly List<ChatHit> hits = new(MaxHits);
    private string query = string.Empty;

    public IReadOnlyList<ChatHit> Hits => hits;

    public bool Active => query.Length >= MinQueryLength;

    public void Clear()
    {
        query = string.Empty;
        hits.Clear();
    }

    public void Run(string next, ChatInbox inbox, ChatLog log)
    {
        var trimmed = next.Trim();
        if (string.Equals(trimmed, query, StringComparison.Ordinal))
        {
            return;
        }

        query = trimmed;
        hits.Clear();
        if (trimmed.Length < MinQueryLength)
        {
            return;
        }

        Collect(inbox.Pinned, log, trimmed);
        Collect(inbox.Rows, log, trimmed);
        hits.Sort(static (left, right) => right.Entry.At.CompareTo(left.Entry.At));
    }

    private void Collect(IReadOnlyList<InboxRow> rows, ChatLog log, string needle)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (hits.Count >= MaxHits)
            {
                return;
            }

            var row = rows[index];
            if (row.Tab is { } tab)
            {
                for (var channelIndex = 0; channelIndex < tab.Channels.Count; channelIndex++)
                {
                    Scan(row, log.Lines(tab.Channels[channelIndex]), needle);
                }

                continue;
            }

            Scan(row, log.Lines(row.StreamKey), needle);
        }
    }

    private void Scan(InboxRow row, IReadOnlyList<ChatEntry> lines, string needle)
    {
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            if (hits.Count >= MaxHits)
            {
                return;
            }

            var entry = lines[index];
            if (Matches(entry, needle))
            {
                hits.Add(new ChatHit(row.Key, row.Title, entry));
            }
        }
    }

    private static bool Matches(ChatEntry entry, string needle) =>
        entry.Text.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
        entry.AuthorName.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
