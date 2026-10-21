using Aetherphone.Core.Localization;

namespace Aetherphone.Core.News;

internal enum MaintenanceStatus : byte
{
    None,
    Upcoming,
    Active,
    Done,
}

internal static class NewsFormat
{
    private const string ParagraphBreak = "\n\n";
    private const string MetaSeparator = " · ";

    public static string Window(DateTimeOffset start, DateTimeOffset end)
    {
        var localStart = start.ToLocalTime();
        var localEnd = end.ToLocalTime();
        var startText = localStart.ToString("MMM d, " + TimeText.ClockPattern, Loc.Culture);
        var endText = localStart.Date == localEnd.Date
            ? TimeText.Clock(localEnd)
            : localEnd.ToString("MMM d, " + TimeText.ClockPattern, Loc.Culture);
        return string.Concat(startText, " – ", endText);
    }

    public static string Window(in MaintenanceWindow window) =>
        Window(DateTimeOffset.FromUnixTimeSeconds(window.StartUnix), DateTimeOffset.FromUnixTimeSeconds(window.EndUnix));

    public static string LongDate(DateTimeOffset moment)
    {
        var local = moment.ToLocalTime();
        return string.Concat(local.ToString("D", Loc.Culture), MetaSeparator, TimeText.Clock(local));
    }

    public static string[] Paragraphs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return normalized.Split(ParagraphBreak, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
