namespace Aetherphone.Core.Venues;

internal readonly record struct VenueHoursSlot(int Day, DateTime StartLocal, DateTime? EndLocal, bool Now);

internal static class VenueHours
{
    public const int DayCount = 7;

    public static DateTime Today(DateTime nowUtc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone).Date;

    public static void Collect(IReadOnlyList<VenueOpening> openings, DateTime nowUtc, TimeZoneInfo zone,
        List<VenueHoursSlot> into)
    {
        into.Clear();
        var today = Today(nowUtc, zone);
        for (var index = 0; index < openings.Count; index++)
        {
            var opening = openings[index];
            var start = TimeZoneInfo.ConvertTimeFromUtc(opening.StartUtc, zone);
            DateTime? end = opening.EndUtc is { } endUtc ? TimeZoneInfo.ConvertTimeFromUtc(endUtc, zone) : null;
            var ongoing = opening.StartUtc <= nowUtc && (opening.EndUtc is not { } until || until > nowUtc);
            var day = (int)(start.Date - today).TotalDays;
            if (day < 0 && ongoing)
            {
                day = 0;
            }

            if (day < 0 || day >= DayCount)
            {
                continue;
            }

            into.Add(new VenueHoursSlot(day, start, end, ongoing));
        }

        into.Sort(static (left, right) => left.StartLocal.CompareTo(right.StartLocal));
    }
}
