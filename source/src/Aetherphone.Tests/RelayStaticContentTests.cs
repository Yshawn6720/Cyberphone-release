using System.Text;
using Aetherphone.Core.Game;
using Aetherphone.Core.Net;
using Aetherphone.Core.News;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Telephony;
using Dalamud.Game;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RelayStaticContentTests : IDisposable
{
    private const string Invite = "relay-invite";
    private const string Tag = "\"fedcba9876543210\"";
    private const string NewsBody = "{\"updatedAt\":\"2026-10-22T23:51:14+08:00\",\"topics\":["
        + "{\"id\":\"tw-1\",\"url\":\"https://www.ffxiv.com.tw/web/news/news_content.aspx?id=a\",\"title\":\"Older\",\"time\":\"2026-10-20T10:00:00+08:00\"},"
        + "{\"id\":\"tw-2\",\"url\":\"https://www.ffxiv.com.tw/web/news/news_content.aspx?id=b\",\"title\":\"Newer\",\"time\":\"2026-10-21T10:00:00+08:00\",\"image\":\"https://www.ffxiv.com.tw/banner.png\"}"
        + "],\"notices\":[],\"maintenance\":[],\"updates\":[]}";
    private const string RadioBody = "{\"stations\":[{\"name\":\"Groove Salad\",\"streamUrl\":\"https://ice1.somafm.com/groovesalad-128-mp3\","
        + "\"genre\":\"Ambient\",\"homepage\":\"https://somafm.com/groovesalad/\"}]}";

    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "aetherphone-relay-static-" + Guid.NewGuid().ToString("N"));
    private readonly HttpService http = new();
    private DateTimeOffset now = new(2026, 10, 22, 16, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        http.Dispose();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(8u, ClientLanguage.English, GameData.TaiwanLocale)]
    [InlineData(5u, ClientLanguage.English, GameData.ChineseLocale)]
    [InlineData(1u, ClientLanguage.Japanese, "jp")]
    [InlineData(3u, ClientLanguage.French, "fr")]
    [InlineData(3u, ClientLanguage.German, "de")]
    [InlineData(3u, ClientLanguage.English, "eu")]
    [InlineData(2u, ClientLanguage.English, "na")]
    [InlineData(0u, ClientLanguage.English, "na")]
    public void TheNewsLocaleFollowsTheRegionOfTheWorld(uint regionId, ClientLanguage language, string expected)
    {
        Assert.Equal(expected, GameData.LocaleFor(regionId, language));
    }

    [Fact]
    public void TheTraditionalChineseClientHasNoLodestoneStatusTab()
    {
        Assert.False(NewsCategories.AvailableFor(NewsCategory.Status, GameData.TaiwanLocale));
        Assert.True(NewsCategories.AvailableFor(NewsCategory.Topics, GameData.TaiwanLocale));
    }

    [Fact]
    public void TheTaiwanBoardKeepsOnlyReadableLinksNewestFirstWithoutImages()
    {
        var published = new DateTimeOffset(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);
        var document = new TaiwanNewsDocument
        {
            Topics =
            [
                Story("tw-1", "https://www.ffxiv.com.tw/a", published.AddDays(-1)),
                Story("tw-2", "https://www.ffxiv.com.tw/b", published, "https://www.ffxiv.com.tw/banner.png"),
                Story("tw-3", "javascript:alert(1)", published.AddDays(1)),
                Story("tw-4", "https://www.ffxiv.com.tw/d", default),
                Story(string.Empty, "https://www.ffxiv.com.tw/e", published),
            ],
        };

        var board = TaiwanNewsBoard.From(document);
        var topics = board.ItemsFor(NewsCategory.Topics);

        Assert.Equal(new[] { "tw-2", "tw-1" }, Array.ConvertAll(topics, item => item.Id));
        Assert.All(topics, item => Assert.Null(item.Image));
        Assert.Empty(board.ItemsFor(NewsCategory.Notices));
        Assert.Empty(board.ItemsFor(NewsCategory.Status));
    }

    [Theory]
    [InlineData("", Invite)]
    [InlineData("loopback", "")]
    public void TaiwanNewsWithoutARelayShowsNothingAndNeverConnects(string url, string invite)
    {
        using var server = new LoopbackRelayServer(respond: Answer);
        var configuration = new Configuration
        {
            PartyRelayUrl = url == "loopback" ? server.Url : url,
            PartyRelayInvite = invite,
        };
        using var source = CreateNews(configuration);

        var entry = source.Request(NewsCategory.Topics, true, true);

        Assert.Equal(NewsState.Unconfigured, entry.State);
        Assert.Empty(entry.Items);
        Assert.Equal(0, server.ConnectionCount);
    }

    [Fact]
    public async Task TaiwanNewsLoadsFromTheRelayOnlyWhenThePageMayConnect()
    {
        using var server = new LoopbackRelayServer(respond: Answer);
        using var source = CreateNews(Configured(server));

        var quiet = source.Request(NewsCategory.Topics, false, false);
        Assert.Equal(NewsState.Empty, quiet.State);
        Assert.Equal(0, server.ConnectionCount);

        var loading = source.Request(NewsCategory.Topics, false, true);
        Assert.Equal(NewsState.Loading, loading.State);
        await source.Settled;
        var ready = source.Request(NewsCategory.Topics, false, true);

        Assert.Equal(NewsState.Ready, ready.State);
        Assert.Equal(new[] { "tw-2", "tw-1" }, Array.ConvertAll(ready.Items, item => item.Id));
        Assert.Null(ready.Items[0].Image);
        Assert.Equal(new DateTime(2026, 10, 22, 15, 51, 14, DateTimeKind.Utc), ready.LastSuccessUtc);
        var request = Assert.Single(server.Requests);
        Assert.Equal("GET /content/news-tw HTTP/1.1", LoopbackRelayServer.RequestLine(request));
        Assert.Equal("Bearer " + Invite, LoopbackRelayServer.HeaderValue(request, "Authorization"));

        now += TimeSpan.FromHours(1);
        source.Request(NewsCategory.Topics, true, true);
        await source.Settled;
        Assert.Single(server.Requests);
    }

    [Fact]
    public void TheRelayRadioListKeepsPlayableStationsOnce()
    {
        var document = new RelayRadioDocument
        {
            Stations =
            [
                new RelayRadioEntry
                {
                    Name = " Groove Salad ", StreamUrl = "https://ice1.somafm.com/groovesalad-128-mp3", Genre = "Ambient",
                    Homepage = "https://somafm.com/groovesalad/",
                },
                new RelayRadioEntry
                {
                    Name = "Duplicate", StreamUrl = "https://ice1.somafm.com/groovesalad-128-mp3", Genre = "Ambient",
                    Homepage = "https://somafm.com/",
                },
                new RelayRadioEntry { Name = "No stream", StreamUrl = "file:///C:/music.mp3", Genre = "Rock" },
                new RelayRadioEntry { Name = " ", StreamUrl = "https://radio.example.com/live" },
                new RelayRadioEntry
                {
                    Name = "KEXP", StreamUrl = "https://kexp.streamguys1.com/kexp160.aac", Genre = "Eclectic",
                    Homepage = "ftp://kexp.org",
                },
            ],
        };

        var list = RelayRadioStations.Readable(document);

        Assert.Equal(new[] { "Groove Salad", "KEXP" }, Array.ConvertAll(list.Stations, station => station.Name));
        Assert.Equal("Ambient", list.Stations[0].Tags);
        Assert.Equal(new[] { "https://somafm.com/groovesalad/", string.Empty }, list.Homepages);
        Assert.Empty(RelayRadioStations.Readable(new RelayRadioDocument()).Stations);
    }

    [Fact]
    public async Task TheRelayRadioListStaysHiddenWithoutARelayAndLoadsWithOne()
    {
        using var server = new LoopbackRelayServer(respond: Answer);
        using var hidden = CreateRadio(new Configuration { PartyRelayUrl = server.Url, PartyRelayInvite = string.Empty });
        Assert.False(hidden.EnsureFresh());
        Assert.Empty(hidden.List.Stations);
        Assert.Equal(0, server.ConnectionCount);

        using var radio = CreateRadio(Configured(server));
        Assert.True(radio.EnsureFresh());
        await radio.Settled;
        Assert.True(radio.EnsureFresh());

        var station = Assert.Single(radio.List.Stations);
        Assert.Equal("Groove Salad", station.Name);
        Assert.Equal("https://somafm.com/groovesalad/", Assert.Single(radio.List.Homepages));
        Assert.False(radio.Failed);
        var request = Assert.Single(server.Requests);
        Assert.Equal("GET /content/radio HTTP/1.1", LoopbackRelayServer.RequestLine(request));
    }

    [Theory]
    [InlineData(404, true)]
    [InlineData(401, false)]
    public async Task ARelayWithoutTheNewRoutesReadsAsUnsupportedRatherThanOffline(int status, bool unsupported)
    {
        using var server = new LoopbackRelayServer(refusalStatus: status);
        var configuration = Configured(server);

        using var news = CreateNews(configuration);
        news.Request(NewsCategory.Topics, true, true);
        await news.Settled;
        var entry = news.Request(NewsCategory.Topics, false, true);
        Assert.Equal(unsupported ? NewsState.Unsupported : NewsState.Failed, entry.State);
        Assert.Empty(entry.Items);

        using var radio = CreateRadio(configuration);
        radio.EnsureFresh();
        await radio.Settled;
        Assert.Equal(unsupported, radio.Unsupported);
        Assert.True(radio.Failed);
        Assert.Empty(radio.List.Stations);

        var nearby = new RelayNearbyQuery(http, configuration, PartyRelayKind.Party);
        Assert.True(nearby.TryAsk(132, 73, 0));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (nearby.Loading && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.False(nearby.Loading);
        Assert.Equal(unsupported, nearby.Unsupported);
        Assert.False(nearby.TryTake(out _));
        Assert.Equal(3, server.Requests.Length);
    }

    private static LodestoneNewsItem Story(string id, string url, DateTimeOffset published, string? image = null) =>
        new() { Id = id, Url = url, Title = "Title " + id, Time = published, Image = image };

    private static Configuration Configured(LoopbackRelayServer server) =>
        new() { PartyRelayUrl = server.Url, PartyRelayInvite = Invite };

    private TaiwanNewsSource CreateNews(Configuration configuration) =>
        new(http, configuration, Path.Combine(directory, TaiwanNewsSource.CacheFileName), () => now);

    private RelayRadioStations CreateRadio(Configuration configuration) =>
        new(http, configuration, Path.Combine(directory, RelayRadioStations.CacheFileName), () => now);

    private static byte[] Answer(string request)
    {
        var body = LoopbackRelayServer.RequestLine(request).Contains("/content/radio", StringComparison.Ordinal)
            ? RadioBody
            : NewsBody;
        var payload = Encoding.UTF8.GetBytes(body);
        var head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n"
            + $"ETag: {Tag}\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        return [.. head, .. payload];
    }
}
