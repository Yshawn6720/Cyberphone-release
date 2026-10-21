namespace Aetherphone.Core.Notes;

[Serializable]
internal sealed class PhoneNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Body { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool Pinned { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool HasContent
    {
        get
        {
            var body = Body;
            for (var index = 0; index < body.Length; index++)
            {
                if (!char.IsWhiteSpace(body[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public string Title()
    {
        var body = Body;
        for (var index = 0; index < body.Length; index++)
        {
            var lineEnd = body.IndexOf('\n', index);
            var line = lineEnd < 0 ? body.Substring(index) : body.Substring(index, lineEnd - index);
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }

            if (lineEnd < 0)
            {
                break;
            }

            index = lineEnd;
        }

        return string.Empty;
    }

    public string Preview()
    {
        var title = Title();
        if (title.Length == 0)
        {
            return string.Empty;
        }

        var titleIndex = Body.IndexOf(title, StringComparison.Ordinal);
        var start = titleIndex < 0 ? 0 : titleIndex + title.Length;
        return CollapseWhitespace(Body.AsSpan(start));
    }

    private static string CollapseWhitespace(ReadOnlySpan<char> text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var pendingSpace = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
