namespace Aetherphone.Core.GameChat;

internal sealed class ChatStreamView : IDisposable
{
    private const int MaxLines = ChatLog.MaxLinesPerStream;

    private static readonly Comparison<ChatEntry> ByTime = static (left, right) =>
        left.At != right.At ? left.At.CompareTo(right.At) : left.Sequence.CompareTo(right.Sequence);

    private readonly ChatLog log;
    private readonly List<ChatEntry> merged = new(256);
    private readonly List<string> streams = new(8);
    private long expectedRevision = -1;
    private bool stale = true;

    public ChatStreamView(ChatLog log)
    {
        this.log = log;
        log.Appended += OnAppended;
    }

    public IReadOnlyList<ChatEntry> Entries => merged;

    public long Revision { get; private set; }

    public void Target(ReadOnlySpan<string> streamKeys)
    {
        if (Same(streamKeys))
        {
            return;
        }

        streams.Clear();
        for (var index = 0; index < streamKeys.Length; index++)
        {
            streams.Add(streamKeys[index]);
        }

        stale = true;
    }

    public void Sync()
    {
        if (!stale && expectedRevision == log.Revision)
        {
            return;
        }

        merged.Clear();
        for (var index = 0; index < streams.Count; index++)
        {
            var lines = log.Lines(streams[index]);
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                merged.Add(lines[lineIndex]);
            }
        }

        if (streams.Count > 1)
        {
            merged.Sort(ByTime);
        }

        Trim();
        expectedRevision = log.Revision;
        stale = false;
        Revision++;
    }

    public void Dispose() => log.Appended -= OnAppended;

    private void OnAppended(ChatEntry entry)
    {
        expectedRevision = log.Revision;
        if (stale || !Owns(entry.StreamKey))
        {
            return;
        }

        merged.Add(entry);
        Trim();
        Revision++;
    }

    private bool Owns(string streamKey)
    {
        for (var index = 0; index < streams.Count; index++)
        {
            if (string.Equals(streams[index], streamKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool Same(ReadOnlySpan<string> streamKeys)
    {
        if (streamKeys.Length != streams.Count)
        {
            return false;
        }

        for (var index = 0; index < streams.Count; index++)
        {
            if (!string.Equals(streams[index], streamKeys[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void Trim()
    {
        if (merged.Count > MaxLines)
        {
            merged.RemoveRange(0, merged.Count - MaxLines);
        }
    }
}
