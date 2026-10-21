using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Venues;

internal enum VenueStatusKind : byte
{
    None,
    Upcoming,
    Open,
    Live,
}

internal readonly record struct VenueStatus(VenueStatusKind Kind, string Label);

internal static class VenueFormat
{
    public static VenueStatus Status(VenueEvent venue, DateTime nowUtc) =>
        Describe(venue.StartUtc, venue.EndUtc, venue.IsConfirmedLive(nowUtc), venue.IsScheduledOpen(nowUtc),
            (venue.Sources & VenueSources.Partake) != 0, nowUtc);

    public static VenueStatus EventStatus(VenueEvent venue, DateTime nowUtc)
    {
        var on = venue.IsEventOn(nowUtc);
        return Describe(venue.EventStartUtc, venue.EventEndUtc, on && venue.IsConfirmedLive(nowUtc), on, true,
            nowUtc);
    }

    public static string Performer(VenueEvent venue, DateTime nowUtc)
    {
        if (!venue.IsConfirmedLive(nowUtc))
        {
            return string.Empty;
        }

        var headline = VenueDisplayText.Clean(venue.LiveHeadline);
        return headline.Length > 0 ? Loc.T(L.Venues.LivePerformer, headline) : string.Empty;
    }

    private static VenueStatus Describe(DateTime? startUtc, DateTime? endUtc, bool live, bool open, bool starts,
        DateTime nowUtc)
    {
        if (live || open)
        {
            var ends = EndsAt(endUtc, nowUtc);
            return new VenueStatus(live ? VenueStatusKind.Live : VenueStatusKind.Open,
                ends.Length > 0 ? Loc.T(L.Venues.OpenUntil, ends) : Loc.T(L.Venues.OpenNow));
        }

        if (startUtc is not { } start || start <= nowUtc)
        {
            return new VenueStatus(VenueStatusKind.None, string.Empty);
        }

        var moment = TimeText.FutureMoment(new DateTimeOffset(start).ToUnixTimeSeconds());
        var label = starts ? Loc.T(L.Venues.StartsAt, moment) : Loc.T(L.Venues.OpensAt, moment);
        return new VenueStatus(VenueStatusKind.Upcoming, label);
    }

    public static string Meta(VenueEvent venue)
    {
        var world = venue.World.Length > 0 && venue.DataCenter.Length > 0
            ? $"{venue.World} ({venue.DataCenter})"
            : venue.World.Length > 0 ? venue.World : venue.DataCenter;
        return VenueMapper.BuildPlaceLine(world, venue.LocationLine);
    }

    public static string Viewers(int count) => Loc.T(L.Venues.Watching, count.ToString("N0", Loc.Culture));

    public static string Window(DateTime startLocal, DateTime? endLocal) =>
        endLocal is { } end ? $"{TimeText.Clock(startLocal)} – {TimeText.Clock(end)}" : TimeText.Clock(startLocal);

    private static string EndsAt(DateTime? endUtc, DateTime nowUtc) =>
        endUtc is { } end && end > nowUtc ? TimeText.Clock(end.ToLocalTime()) : string.Empty;
}
