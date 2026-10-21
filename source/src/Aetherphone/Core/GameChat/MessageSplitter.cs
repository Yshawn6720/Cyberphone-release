using System.Text;

namespace Aetherphone.Core.GameChat;

internal static class MessageSplitter
{
    public const int MaxParts = 5;
    private const int MinimumPartBytes = 8;

    public static int Capacity(int budget, string indicator)
    {
        if (budget <= 0)
        {
            return 0;
        }

        var suffix = SuffixFor(indicator, budget);
        return (budget - Encoding.UTF8.GetByteCount(suffix)) * MaxParts;
    }

    public static void Split(string text, int budget, string indicator, List<string> parts)
    {
        parts.Clear();
        if (budget <= 0 || text.Length == 0)
        {
            return;
        }

        var suffix = SuffixFor(indicator, budget);
        var suffixBytes = Encoding.UTF8.GetByteCount(suffix);
        var cursor = SkipWhitespace(text, 0);
        while (cursor < text.Length && parts.Count < MaxParts)
        {
            var lineEnd = LineEnd(text, cursor);
            var take = TakeUpTo(text, cursor, lineEnd, budget);
            if (take < lineEnd || !IsTail(text, lineEnd))
            {
                take = TakeUpTo(text, cursor, lineEnd, budget - suffixBytes);
            }

            if (take <= cursor)
            {
                break;
            }

            Add(parts, text, cursor, take);
            cursor = SkipWhitespace(text, take);
        }

        for (var index = 0; index < parts.Count - 1; index++)
        {
            parts[index] = string.Concat(parts[index], suffix);
        }
    }

    private static string SuffixFor(string indicator, int budget)
    {
        var trimmed = indicator.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var suffix = string.Concat(" ", trimmed);
        var bytes = Encoding.UTF8.GetByteCount(suffix);
        return budget - bytes >= MinimumPartBytes ? suffix : string.Empty;
    }

    private static void Add(List<string> parts, string text, int start, int end)
    {
        var span = text.AsSpan(start, end - start).TrimEnd();
        if (span.Length == 0)
        {
            return;
        }

        parts.Add(span.ToString());
    }

    private static int TakeUpTo(string text, int start, int stop, int limit)
    {
        if (limit <= 0)
        {
            return start;
        }

        var bytes = 0;
        var index = start;
        var lastBreak = -1;
        while (index < stop)
        {
            var runeLength = RuneLength(text, index);
            var runeBytes = Encoding.UTF8.GetByteCount(text.AsSpan(index, runeLength));
            if (bytes + runeBytes > limit)
            {
                break;
            }

            if (char.IsWhiteSpace(text[index]))
            {
                lastBreak = index;
            }

            bytes += runeBytes;
            index += runeLength;
        }

        if (index >= stop)
        {
            return stop;
        }

        return lastBreak > start ? lastBreak : index;
    }

    private static bool IsTail(string text, int from)
    {
        for (var index = from; index < text.Length; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static int LineEnd(string text, int from)
    {
        var found = text.IndexOf('\n', from);
        return found < 0 ? text.Length : found;
    }

    private static int SkipWhitespace(string text, int from)
    {
        var index = from;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int RuneLength(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
}
