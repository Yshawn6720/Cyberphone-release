using Aetherphone.Core.Venues;

namespace Aetherphone.Apps.Venues;

internal enum VenueScreen : byte
{
    Home,
    Filters,
    Detail,
    List,
    Scope,
}

internal enum VenueTab : byte
{
    Discover,
    Live,
    Events,
    Saved,
}

internal enum VenueListKind : byte
{
    Live,
    LaterToday,
    NearYou,
    Category,
    Directory,
}

internal readonly record struct VenueRoute(VenueScreen Screen, VenueEvent? Venue = null,
    VenueListKind List = VenueListKind.Directory, int Category = -1, string BackTitle = "")
{
    public static readonly VenueRoute Home = new(VenueScreen.Home);

    public static VenueRoute Filters(string backTitle) => new(VenueScreen.Filters, BackTitle: backTitle);

    public static VenueRoute Scope(string backTitle) => new(VenueScreen.Scope, BackTitle: backTitle);

    public static VenueRoute Detail(VenueEvent venue, string backTitle) =>
        new(VenueScreen.Detail, venue, BackTitle: backTitle);

    public static VenueRoute ListOf(VenueListKind kind, int category, string backTitle) =>
        new(VenueScreen.List, null, kind, category, backTitle);
}
