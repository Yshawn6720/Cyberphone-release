namespace Aetherphone.Core.Emoji;

internal static class EmojiAutocomplete
{
    public const int MinimumQuery = 2;
    public const int MaxResults = 8;

    private const int ScoreExact = 0;
    private const int ScorePrefix = 1;
    private const int ScoreContains = 2;
    private const int ScoreNone = -1;

    public static bool TryToken(ReadOnlySpan<char> text, int caret, out int start, out int length)
    {
        start = 0;
        length = 0;
        if (caret <= 0 || caret > text.Length)
        {
            return false;
        }

        var cursor = caret;
        while (cursor > 0 && EmojiScanner.IsShortcodeChar(text[cursor - 1]))
        {
            cursor--;
        }

        if (cursor == 0 || text[cursor - 1] != ':')
        {
            return false;
        }

        var colon = cursor - 1;
        if (colon > 0 && !char.IsWhiteSpace(text[colon - 1]))
        {
            return false;
        }

        start = colon;
        length = caret - cursor;
        return length > 0;
    }

    public static int Rank(ReadOnlySpan<char> query, Span<EmojiShortcode> results) =>
        Rank(EmojiCatalog.Shortcodes, query, results);

    public static int Rank(ReadOnlySpan<EmojiShortcode> table, ReadOnlySpan<char> query, Span<EmojiShortcode> results)
    {
        if (query.Length == 0 || results.Length == 0)
        {
            return 0;
        }

        var limit = Math.Min(results.Length, MaxResults);
        Span<byte> scores = stackalloc byte[MaxResults];
        var count = 0;
        for (var index = 0; index < table.Length; index++)
        {
            var score = Score(table[index].Code, query);
            if (score == ScoreNone)
            {
                continue;
            }

            if (count == limit && score >= scores[count - 1])
            {
                continue;
            }

            var slot = count < limit ? count : limit - 1;
            while (slot > 0 && scores[slot - 1] > score)
            {
                slot--;
            }

            for (var shift = count < limit ? count : limit - 1; shift > slot; shift--)
            {
                results[shift] = results[shift - 1];
                scores[shift] = scores[shift - 1];
            }

            results[slot] = table[index];
            scores[slot] = (byte)score;
            if (count < limit)
            {
                count++;
            }
        }

        return count;
    }

    private static int Score(ReadOnlySpan<char> code, ReadOnlySpan<char> query)
    {
        if (code.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return ScoreExact;
        }

        if (code.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return ScorePrefix;
        }

        if (code.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return ScoreContains;
        }

        return ScoreNone;
    }
}
