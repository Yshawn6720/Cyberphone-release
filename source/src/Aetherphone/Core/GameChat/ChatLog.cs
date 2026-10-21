namespace Aetherphone.Core.GameChat;

internal sealed class ChatLog
{
    public const int MaxLinesPerStream = 2000;
    private const int TrimBlock = 128;

    private static readonly ChatEntry[] NoLines = Array.Empty<ChatEntry>();

    private readonly Dictionary<string, List<ChatEntry>> byStream = new(StringComparer.Ordinal);
    private long sequence;

    public long Revision { get; private set; }

    public event Action<ChatEntry>? Appended;

    public long NextSequence() => ++sequence;

    public void Append(ChatEntry entry)
    {
        Buffer(entry.StreamKey).Add(entry);
        Trim(entry.StreamKey);
        Revision++;
        Appended?.Invoke(entry);
    }

    public void Restore(string streamKey, List<ChatEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var lines = Buffer(streamKey);
        lines.AddRange(entries);
        lines.Sort(static (left, right) => left.At != right.At
            ? left.At.CompareTo(right.At)
            : left.Sequence.CompareTo(right.Sequence));
        Trim(streamKey);
        Revision++;
    }

    public IReadOnlyList<ChatEntry> Lines(string streamKey) =>
        byStream.TryGetValue(streamKey, out var lines) ? lines : NoLines;

    public bool HasLines(string streamKey) => byStream.TryGetValue(streamKey, out var lines) && lines.Count > 0;

    public void CollectStreams(List<string> into)
    {
        into.Clear();
        foreach (var stream in byStream)
        {
            if (stream.Value.Count > 0)
            {
                into.Add(stream.Key);
            }
        }
    }

    public void Clear()
    {
        if (byStream.Count == 0)
        {
            return;
        }

        byStream.Clear();
        Revision++;
    }

    public void Clear(string streamKey)
    {
        if (!byStream.Remove(streamKey))
        {
            return;
        }

        Revision++;
    }

    private List<ChatEntry> Buffer(string streamKey)
    {
        if (byStream.TryGetValue(streamKey, out var lines))
        {
            return lines;
        }

        lines = new List<ChatEntry>(64);
        byStream[streamKey] = lines;
        return lines;
    }

    private void Trim(string streamKey)
    {
        var lines = byStream[streamKey];
        if (lines.Count > MaxLinesPerStream + TrimBlock)
        {
            lines.RemoveRange(0, lines.Count - MaxLinesPerStream);
        }
    }
}
