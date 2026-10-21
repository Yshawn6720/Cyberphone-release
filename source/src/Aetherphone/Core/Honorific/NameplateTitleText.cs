using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Aetherphone.Core.Honorific;

internal static class NameplateTitleText
{
    public const int MaxLength = 32;
    public const string Separator = " · ";
    public const char LineBreak = '\n';
    private const char Ellipsis = '…';
    private const char HandleMark = '@';
    private const char TokenOpen = '[';
    private const char TokenClose = ']';
    private const string ProbeText = "X";
    private const int MinimumPageLength = 8;

    public static string Fit(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxLength)
        {
            return trimmed;
        }

        return string.Concat(trimmed.AsSpan(0, MaxLength - 1).TrimEnd(), Ellipsis.ToString());
    }

    public static string Render(string template, in NameplateValues values)
    {
        if (template.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(template.Length + 32);
        var index = 0;
        while (index < template.Length)
        {
            var character = template[index];
            var close = character == TokenOpen ? template.IndexOf(TokenClose, index + 1) : -1;
            if (close < 0)
            {
                builder.Append(character);
                index++;
                continue;
            }

            var token = template.AsSpan(index, close - index + 1);
            if (!TryValue(token, values, out var value))
            {
                builder.Append(token);
                index = close + 1;
                continue;
            }

            index = close + 1;
            if (value.Length > 0)
            {
                builder.Append(value.Trim());
                continue;
            }

            if (!TrimTrailingSeparator(builder) &&
                template.AsSpan(index).StartsWith(Separator, StringComparison.Ordinal))
            {
                index += Separator.Length;
            }
        }

        return Clean(builder.ToString());
    }

    public static string[] Turns(string template, in NameplateValues values, bool shorten)
    {
        var turns = new List<string>(4);
        var start = 0;
        while (start <= template.Length)
        {
            var end = template.IndexOf(LineBreak, start);
            if (end < 0)
            {
                end = template.Length;
            }

            AddLineTurns(turns, template.Substring(start, end - start), values, shorten);
            start = end + 1;
        }

        return turns.ToArray();
    }

    private static void AddLineTurns(List<string> turns, string line, in NameplateValues values, bool shorten)
    {
        if (shorten)
        {
            AddIfShown(turns, RenderFitted(line, values));
            return;
        }

        var whole = Render(line, values);
        if (whole.Length <= MaxLength)
        {
            AddIfShown(turns, whole);
            return;
        }

        var count = turns.Count;
        AddPages(turns, line, values with { Artist = string.Empty }, values.Song, false);
        AddPages(turns, line, values with { Song = string.Empty }, values.Artist, true);
        if (turns.Count == count)
        {
            AddTextPages(turns, whole);
        }
    }

    private static void AddIfShown(List<string> turns, string text)
    {
        if (text.Length > 0)
        {
            turns.Add(text);
        }
    }

    private static void AddTextPages(List<string> turns, string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var length = PageLength(text, start, MaxLength);
            AddIfShown(turns, Clean(text.Substring(start, length)));
            start = SkipSpaces(text, start + length);
        }
    }

    private static int SkipSpaces(string text, int index)
    {
        while (index < text.Length && text[index] == ' ')
        {
            index++;
        }

        return index;
    }

    private static void AddPages(List<string> turns, string template, in NameplateValues values, string text,
        bool artist)
    {
        var token = artist ? NameplateStatusCatalog.ArtistToken : NameplateStatusCatalog.SongToken;
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || !template.Contains(token, StringComparison.Ordinal))
        {
            return;
        }

        var budget = MaxLength - (Render(template, With(values, artist, ProbeText)).Length - ProbeText.Length);
        if (budget < MinimumPageLength)
        {
            turns.Add(Fit(Render(template, With(values, artist, trimmed))));
            return;
        }

        var start = 0;
        while (start < trimmed.Length)
        {
            var length = PageLength(trimmed, start, budget);
            AddIfShown(turns, Render(template, With(values, artist, trimmed.Substring(start, length))));
            start = SkipSpaces(trimmed, start + length);
        }
    }

    private static int PageLength(string text, int start, int budget)
    {
        var remaining = text.Length - start;
        if (remaining <= budget)
        {
            return remaining;
        }

        var space = text.LastIndexOf(' ', start + budget, budget + 1);
        return space > start ? space - start : budget;
    }

    private static NameplateValues With(in NameplateValues values, bool artist, string text) =>
        artist ? values with { Artist = text } : values with { Song = text };

    public static string RenderFitted(string template, in NameplateValues values)
    {
        var text = Render(template, values);
        if (text.Length > MaxLength && values.Artist.Length > 0)
        {
            text = Render(template, values with { Artist = string.Empty });
        }

        return Fit(text);
    }

    private static bool TryValue(ReadOnlySpan<char> token, in NameplateValues values, out string value)
    {
        value = token switch
        {
            NameplateStatusCatalog.CodeToken => values.Code,
            NameplateStatusCatalog.NameToken => values.Name,
            NameplateStatusCatalog.StationToken => values.Station,
            NameplateStatusCatalog.TypeToken => values.Type,
            NameplateStatusCatalog.HandleToken => values.Handle.TrimStart(HandleMark),
            NameplateStatusCatalog.SongToken => values.Song,
            NameplateStatusCatalog.ArtistToken => values.Artist,
            NameplateStatusCatalog.GameToken => values.Game,
            NameplateStatusCatalog.ChipsToken => values.Chips,
            _ => null!,
        };
        return value is not null;
    }

    private static bool TrimTrailingSeparator(StringBuilder builder)
    {
        var start = builder.Length - Separator.Length;
        if (start < 0)
        {
            return false;
        }

        for (var offset = 0; offset < Separator.Length; offset++)
        {
            if (builder[start + offset] != Separator[offset])
            {
                return false;
            }
        }

        builder.Length = start;
        return true;
    }

    private static string Clean(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith(Separator.TrimStart(), StringComparison.Ordinal))
        {
            trimmed = trimmed[Separator.TrimStart().Length..].TrimStart();
        }

        for (var index = 0; index < trimmed.Length; index++)
        {
            if (char.IsLetterOrDigit(trimmed[index]))
            {
                return trimmed;
            }
        }

        return string.Empty;
    }

    public static string ToJson(in NameplateTitle title)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("Title", title.Text);
            writer.WriteBoolean("IsPrefix", title.Prefix);
            writer.WriteBoolean("IsOriginal", false);
            WriteVector(writer, "Color", title.Look.Color);
            if (title.Look.Glow is { } glow)
            {
                WriteVector(writer, "Glow", glow);
            }

            if (title.Look.Color3 is { } third)
            {
                WriteVector(writer, "Color3", third);
            }

            if (title.Look.GradientColourSet is { } colourSet)
            {
                writer.WriteNumber("GradientColourSet", colourSet);
            }

            if (title.Look.GradientAnimationStyle is { } animation)
            {
                writer.WriteNumber("GradientAnimationStyle", animation);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static bool TryReadTitle(string json, out string title, out TitleLook look)
    {
        title = string.Empty;
        look = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("IsOriginal", out var original) && original.ValueKind == JsonValueKind.True)
            {
                return false;
            }

            if (root.TryGetProperty("Title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
            {
                title = titleElement.GetString() ?? string.Empty;
            }

            var color = ReadVector(root, "Color") ?? NameplatePalette.Colors[0];
            look = new TitleLook(color, ReadVector(root, "Glow"), ReadVector(root, "Color3"),
                ReadInt(root, "GradientColourSet"), ReadInt(root, "GradientAnimationStyle"));
            return title.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void WriteVector(Utf8JsonWriter writer, string name, Vector3 value)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteEndObject();
    }

    private static Vector3? ReadVector(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new Vector3(ReadComponent(element, "X"), ReadComponent(element, "Y"), ReadComponent(element, "Z"));
    }

    private static float ReadComponent(JsonElement element, string name) =>
        element.TryGetProperty(name, out var component) && component.ValueKind == JsonValueKind.Number
            ? component.GetSingle()
            : 0f;

    private static int? ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number &&
        element.TryGetInt32(out var value)
            ? value
            : null;
}
