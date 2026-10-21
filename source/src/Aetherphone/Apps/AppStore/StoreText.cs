using System.Globalization;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.AppStore;

internal sealed class StoreText
{
    private const string ReleaseDateFormat = "yyyy-MM-dd";
    private const int NoonHour = 12;
    private const int Limit = 64;

    private readonly Dictionary<string, string> versions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> dates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> updatedTitles = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> counts = new();
    private CultureInfo? culture;
    private int timeFormat = -1;
    private DateTime day;
    private string today = string.Empty;

    public string Today()
    {
        Validate();
        if (today.Length == 0)
        {
            today = Loc.Upper(string.Concat(day.ToString("dddd", Loc.Culture), ", ", day.ToString("M", Loc.Culture)));
        }

        return today;
    }

    public string Version(string version)
    {
        Validate();
        return versions.TryGetValue(version, out var cached)
            ? cached
            : Remember(versions, version, Loc.T(L.Store.VersionLabel, version));
    }

    public string UpdatedIn(string version)
    {
        Validate();
        return updatedTitles.TryGetValue(version, out var cached)
            ? cached
            : Remember(updatedTitles, version, Loc.T(L.Store.UpdatedIn, version));
    }

    public string ReleaseDate(string isoDate)
    {
        Validate();
        if (dates.TryGetValue(isoDate, out var cached))
        {
            return cached;
        }

        var text = string.Empty;
        if (DateTime.TryParseExact(isoDate, ReleaseDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsed))
        {
            var local = new DateTimeOffset(parsed.Year, parsed.Month, parsed.Day, NoonHour, 0, 0,
                TimeZoneInfo.Local.GetUtcOffset(parsed));
            text = TimeText.DayLabel(local.ToUnixTimeSeconds());
        }

        return Remember(dates, isoDate, text);
    }

    public string Count(int value)
    {
        Validate();
        if (counts.TryGetValue(value, out var cached))
        {
            return cached;
        }

        var text = value.ToString(Loc.Culture);
        counts[value] = text;
        return text;
    }

    private static string Remember(Dictionary<string, string> cache, string key, string text)
    {
        if (cache.Count >= Limit)
        {
            cache.Clear();
        }

        cache[key] = text;
        return text;
    }

    private void Validate()
    {
        var current = DateTime.Now.Date;
        if (ReferenceEquals(culture, Loc.Culture) && timeFormat == TimeText.FormatVersion && current == day)
        {
            return;
        }

        culture = Loc.Culture;
        timeFormat = TimeText.FormatVersion;
        day = current;
        today = string.Empty;
        versions.Clear();
        dates.Clear();
        updatedTitles.Clear();
        counts.Clear();
    }
}
