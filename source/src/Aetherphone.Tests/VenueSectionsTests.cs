using Aetherphone.Core.Venues;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueSectionsTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);
    private static readonly HashSet<string> Light = new(StringComparer.OrdinalIgnoreCase) { "Light" };

    private static VenueEvent Venue(string id, string dataCenter = "Light", string[]? tags = null,
        string? banner = null, DateTime? start = null, DateTime? end = null) =>
        new()
        {
            Id = id,
            Sources = VenueSources.FfxivVenues,
            Title = id,
            Host = string.Empty,
            Description = string.Empty,
            DataCenter = dataCenter,
            World = dataCenter == "Light" ? "Shiva" : "Gilgamesh",
            LocationLine = string.Empty,
            PlaceLine = string.Empty,
            TeleportCode = null,
            BannerUrl = banner,
            LogoUrl = null,
            StartUtc = start,
            EndUtc = end,
            Tags = tags ?? Array.Empty<string>(),
            WebsiteUrl = null,
            DiscordUrl = null,
            ListingUrl = null,
            AttendeeCount = 0,
        };

    private static VenueSections Build(IReadOnlyList<VenueEvent> source, IReadOnlyList<string> favorites,
        IReadOnlyList<string> recents, bool hideAdult = false)
    {
        var sections = new VenueSections();
        var key = new VenueSectionsKey(1, VenueFilter.SourceAll, Light, string.Empty, string.Empty, 0, 0, 0,
            hideAdult);
        sections.Update(key, source, favorites, recents, Array.Empty<string>(), Now);
        return sections;
    }

    [Fact]
    public void Saved_KeepsFavoritesOutsideTheScope()
    {
        var sections = Build([Venue("home"), Venue("away", "Aether")], ["away", "home"], []);

        Assert.Equal(2, sections.Saved.Count);
    }

    [Fact]
    public void Saved_StillHonoursHideAdult()
    {
        var sections = Build([Venue("adult", tags: [VenueMapper.AdultTag]), Venue("safe")], ["adult", "safe"], [],
            hideAdult: true);

        var saved = Assert.Single(sections.Saved);
        Assert.Equal("safe", saved.Id);
    }

    [Fact]
    public void Recents_FollowTheStoredOrderAndSkipMissingVenues()
    {
        var sections = Build([Venue("a"), Venue("b"), Venue("c", "Aether")], [], ["c", "gone", "a"]);

        Assert.Equal(2, sections.Recents.Count);
        Assert.Equal("c", sections.Recents[0].Id);
        Assert.Equal("a", sections.Recents[1].Id);
    }

    [Fact]
    public void Saved_SinksAnOpeningThatAlreadyEndedBelowUpcomingOnes()
    {
        var ended = Venue("ended", start: Now.AddHours(-5), end: Now.AddHours(-1));
        var upcoming = Venue("upcoming", start: Now.AddHours(2), end: Now.AddHours(5));
        var open = Venue("open", start: Now.AddHours(-1), end: Now.AddHours(2));

        var sections = Build([ended, upcoming, open], ["ended", "upcoming", "open"], []);

        Assert.Equal(3, sections.Saved.Count);
        Assert.Same(open, sections.Saved[0]);
        Assert.Same(upcoming, sections.Saved[1]);
        Assert.Same(ended, sections.Saved[2]);
    }

    [Fact]
    public void LaterRail_StaysEmptyWhileTheCarouselAlreadyShowsLaterToday()
    {
        var source = new List<VenueEvent>();
        for (var index = 0; index < VenueSections.MaxFeatured + 3; index++)
        {
            source.Add(Venue("later" + index, start: Now.AddMinutes(1 + index), end: Now.AddHours(3)));
        }

        var sections = Build(source, [], []);

        Assert.False(sections.FeaturedIsLive);
        Assert.NotEmpty(sections.Featured);
        Assert.Empty(sections.LaterRail);
    }

    [Fact]
    public void LaterRail_ListsLaterTodayBelowALiveCarousel()
    {
        var open = Venue("open", start: Now.AddHours(-1), end: Now.AddHours(2));
        var later = Venue("later", start: Now.AddMinutes(1), end: Now.AddHours(3));

        var sections = Build([open, later], [], []);

        Assert.True(sections.FeaturedIsLive);
        Assert.Same(later, Assert.Single(sections.LaterRail));
    }

    [Fact]
    public void CategoryCover_PrefersAnOpenVenueWithABanner()
    {
        var closed = Venue("closed", tags: ["Bar"], banner: "https://example.com/closed.png");
        var open = Venue("open", tags: ["Bar"], banner: "https://example.com/open.png", start: Now.AddHours(-1),
            end: Now.AddHours(2));
        var plain = Venue("plain", tags: ["Bar"], start: Now.AddHours(-1), end: Now.AddHours(2));

        var sections = Build([closed, plain, open], [], []);

        Assert.Equal(3, sections.CategoryCount(VenueCategories.Bars));
        Assert.Same(open, sections.CategoryCover(VenueCategories.Bars));
        Assert.Null(sections.CategoryCover(VenueCategories.Casinos));
    }
}
