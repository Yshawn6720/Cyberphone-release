using Aetherphone.Core;
using Aetherphone.Core.Clock;

namespace Aetherphone.Apps.Clock;

internal readonly record struct WorldCity(string City, string TimeZoneId);

internal static class WorldClockCatalog
{
    public static readonly IReadOnlyList<WorldCity> All = new WorldCity[]
    {
        new("Honolulu", "Hawaiian Standard Time"),
        new("Anchorage", "Alaskan Standard Time"),
        new("Los Angeles", "Pacific Standard Time"),
        new("San Francisco", "Pacific Standard Time"),
        new("Seattle", "Pacific Standard Time"),
        new("Vancouver", "Pacific Standard Time"),
        new("Phoenix", "US Mountain Standard Time"),
        new("Denver", "Mountain Standard Time"),
        new("Chicago", "Central Standard Time"),
        new("Dallas", "Central Standard Time"),
        new("Mexico City", "Central Standard Time (Mexico)"),
        new("New York", "Eastern Standard Time"),
        new("Toronto", "Eastern Standard Time"),
        new("Miami", "Eastern Standard Time"),
        new("Bogota", "SA Pacific Standard Time"),
        new("Lima", "SA Pacific Standard Time"),
        new("Halifax", "Atlantic Standard Time"),
        new("Santiago", "Pacific SA Standard Time"),
        new("Buenos Aires", "Argentina Standard Time"),
        new("Sao Paulo", "E. South America Standard Time"),
        new("Reykjavik", "Greenwich Standard Time"),
        new("London", "GMT Standard Time"),
        new("Dublin", "GMT Standard Time"),
        new("Lisbon", "GMT Standard Time"),
        new("Paris", "Romance Standard Time"),
        new("Madrid", "Romance Standard Time"),
        new("Brussels", "Romance Standard Time"),
        new("Copenhagen", "Romance Standard Time"),
        new("Berlin", "W. Europe Standard Time"),
        new("Amsterdam", "W. Europe Standard Time"),
        new("Rome", "W. Europe Standard Time"),
        new("Stockholm", "W. Europe Standard Time"),
        new("Vienna", "W. Europe Standard Time"),
        new("Zurich", "W. Europe Standard Time"),
        new("Warsaw", "Central European Standard Time"),
        new("Prague", "Central Europe Standard Time"),
        new("Budapest", "Central Europe Standard Time"),
        new("Lagos", "W. Central Africa Standard Time"),
        new("Athens", "GTB Standard Time"),
        new("Bucharest", "GTB Standard Time"),
        new("Helsinki", "FLE Standard Time"),
        new("Kyiv", "FLE Standard Time"),
        new("Cairo", "Egypt Standard Time"),
        new("Johannesburg", "South Africa Standard Time"),
        new("Jerusalem", "Israel Standard Time"),
        new("Istanbul", "Turkey Standard Time"),
        new("Moscow", "Russian Standard Time"),
        new("Riyadh", "Arab Standard Time"),
        new("Nairobi", "E. Africa Standard Time"),
        new("Tehran", "Iran Standard Time"),
        new("Dubai", "Arabian Standard Time"),
        new("Karachi", "Pakistan Standard Time"),
        new("Mumbai", "India Standard Time"),
        new("New Delhi", "India Standard Time"),
        new("Kathmandu", "Nepal Standard Time"),
        new("Dhaka", "Bangladesh Standard Time"),
        new("Bangkok", "SE Asia Standard Time"),
        new("Jakarta", "SE Asia Standard Time"),
        new("Hanoi", "SE Asia Standard Time"),
        new("Singapore", "Singapore Standard Time"),
        new("Kuala Lumpur", "Singapore Standard Time"),
        new("Manila", "Singapore Standard Time"),
        new("Perth", "W. Australia Standard Time"),
        new("Hong Kong", "China Standard Time"),
        new("Shanghai", "China Standard Time"),
        new("Beijing", "China Standard Time"),
        new("Taipei", "Taipei Standard Time"),
        new("Seoul", "Korea Standard Time"),
        new("Tokyo", "Tokyo Standard Time"),
        new("Osaka", "Tokyo Standard Time"),
        new("Adelaide", "Cen. Australia Standard Time"),
        new("Brisbane", "E. Australia Standard Time"),
        new("Sydney", "AUS Eastern Standard Time"),
        new("Melbourne", "AUS Eastern Standard Time"),
        new("Auckland", "New Zealand Standard Time"),
    };

    public static bool TryResolve(string timeZoneId, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Clock] time zone '{timeZoneId}' is unavailable; falling back to UTC");
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    public static bool Contains(List<WorldClockEntry> entries, in WorldCity city)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (string.Equals(entry.City, city.City, StringComparison.Ordinal) &&
                string.Equals(entry.TimeZoneId, city.TimeZoneId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
