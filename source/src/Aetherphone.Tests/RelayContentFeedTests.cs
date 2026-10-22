using System.Text;
using System.Text.Json;
using Aetherphone.Apps.Calendar;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RelayContentFeedTests : IDisposable
{
    private const string Invite = "relay-invite";
    private const string Tag = "\"0123456789abcdef\"";
    private const string Body =
        "{\"items\":[{\"id\":\"welcome\",\"title\":\"Hello\",\"body\":\"World\",\"createdAtUnix\":1792630800}]}";
    private const string LoopbackMarker = "loopback";

    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "aetherphone-relay-content-" + Guid.NewGuid().ToString("N"));
    private readonly HttpService http = new();
    private DateTimeOffset now = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);
    private bool refuse;

    public void Dispose()
    {
        http.Dispose();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(LoopbackMarker, "")]
    [InlineData(LoopbackMarker, "   ")]
    [InlineData("", Invite)]
    [InlineData("https://api.aetherphone.net", Invite)]
    [InlineData("https://ffxiv-aethernet-production.up.railway.app", Invite)]
    [InlineData("http://relay.example.com", Invite)]
    public async Task AnUnconfiguredFeedNeverConnects(string url, string invite)
    {
        using var server = new LoopbackRelayServer(respond: Answer);
        var configuration = new Configuration
        {
            PartyRelayUrl = url == LoopbackMarker ? server.Url : url,
            PartyRelayInvite = invite,
        };
        var feed = CreateFeed(configuration);

        Assert.False(feed.IsConfigured);
        Assert.False(feed.TryGetFresh(out _));
        var result = await feed.LoadAsync(CancellationToken.None);

        Assert.Equal(RelayContentStatus.Unconfigured, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(0, server.ConnectionCount);
        Assert.Empty(server.Requests);
        Assert.False(File.Exists(CachePath));
    }

    [Fact]
    public async Task AFreshCopyIsServedWithoutAskingTheServerAgain()
    {
        using var server = new LoopbackRelayServer(respond: Answer);
        var configuration = Configured(server);
        var feed = CreateFeed(configuration);

        var first = await feed.LoadAsync(CancellationToken.None);
        Assert.Equal(RelayContentStatus.Fetched, first.Status);
        Assert.Equal("welcome", Assert.Single(first.Value!.Items).Id);
        var request = Assert.Single(server.Requests);
        Assert.Equal("GET /content/announcements HTTP/1.1", LoopbackRelayServer.RequestLine(request));
        Assert.Equal("Bearer " + Invite, LoopbackRelayServer.HeaderValue(request, "Authorization"));
        Assert.Null(LoopbackRelayServer.HeaderValue(request, "If-None-Match"));

        now += TimeSpan.FromHours(5);
        var again = await feed.LoadAsync(CancellationToken.None);
        var restarted = CreateFeed(configuration);
        Assert.True(restarted.TryGetFresh(out var cached));
        var afterRestart = await restarted.LoadAsync(CancellationToken.None);

        Assert.Equal(RelayContentStatus.Cached, again.Status);
        Assert.Equal(RelayContentStatus.Cached, afterRestart.Status);
        Assert.Equal("welcome", Assert.Single(cached.Items).Id);
        Assert.Equal("welcome", Assert.Single(afterRestart.Value!.Items).Id);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task AnExpiredCopyIsRevalidatedWithItsETag()
    {
        using var server = new LoopbackRelayServer(respond: Answer);
        var configuration = Configured(server);
        await CreateFeed(configuration).LoadAsync(CancellationToken.None);

        now += RelayContentFeed<AnnouncementPage>.RefreshInterval + TimeSpan.FromMinutes(1);
        var feed = CreateFeed(configuration);
        Assert.False(feed.TryGetFresh(out _));
        var revalidated = await feed.LoadAsync(CancellationToken.None);
        var afterwards = await feed.LoadAsync(CancellationToken.None);

        Assert.Equal(RelayContentStatus.Revalidated, revalidated.Status);
        Assert.Equal("welcome", Assert.Single(revalidated.Value!.Items).Id);
        Assert.Equal(RelayContentStatus.Cached, afterwards.Status);
        var requests = server.Requests;
        Assert.Equal(2, requests.Length);
        Assert.Equal(Tag, LoopbackRelayServer.HeaderValue(requests[1], "If-None-Match"));
    }

    [Fact]
    public async Task ARefusedRefreshKeepsTheLastGoodCopy()
    {
        using var server = new LoopbackRelayServer(respond: Answer);
        var configuration = Configured(server);
        await CreateFeed(configuration).LoadAsync(CancellationToken.None);

        now += RelayContentFeed<AnnouncementPage>.RefreshInterval + TimeSpan.FromMinutes(1);
        Volatile.Write(ref refuse, true);
        var feed = CreateFeed(configuration);
        var result = await feed.LoadAsync(CancellationToken.None);

        Assert.Equal(RelayContentStatus.Failed, result.Status);
        Assert.Equal(401, result.Failure.StatusCode);
        Assert.Equal("welcome", Assert.Single(result.Value!.Items).Id);
        Assert.Equal(2, server.Requests.Length);
    }

    [Fact]
    public void TheShippedServerContentReadsWithTheClientContracts()
    {
        var content = FindServerContent();
        var page = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(content, "announcements.json")),
            AethernetJsonContext.Default.AnnouncementPage);
        var events = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(content, "calendar.json")),
            CalendarJsonContext.Default.EventRecordArray);

        Assert.NotNull(page);
        Assert.NotEmpty(page.Items);
        foreach (var item in page.Items)
        {
            Assert.False(string.IsNullOrEmpty(item.Id));
            Assert.False(string.IsNullOrEmpty(item.Title));
            Assert.False(string.IsNullOrEmpty(item.Body));
            Assert.True(item.CreatedAtUnix > 0);
        }

        Assert.NotNull(events);
        Assert.NotEmpty(events);
        foreach (var record in events)
        {
            Assert.True(record.Id > 0);
            Assert.False(string.IsNullOrEmpty(record.Name));
            Assert.True(record.Begin <= record.End);
            Assert.Equal(DateTimeKind.Local, record.Begin.Kind);
        }
    }

    private string CachePath => Path.Combine(directory, "relay_announcements.json");

    private static Configuration Configured(LoopbackRelayServer server) =>
        new() { PartyRelayUrl = server.Url, PartyRelayInvite = Invite };

    private RelayContentFeed<AnnouncementPage> CreateFeed(Configuration configuration) =>
        new(http, configuration, "announcements", AethernetJsonContext.Default.AnnouncementPage, CachePath, () => now);

    private byte[] Answer(string request)
    {
        if (Volatile.Read(ref refuse))
        {
            return Encoding.ASCII.GetBytes("HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        }

        if (LoopbackRelayServer.HeaderValue(request, "If-None-Match") == Tag)
        {
            return Encoding.ASCII.GetBytes($"HTTP/1.1 304 Not Modified\r\nETag: {Tag}\r\nConnection: close\r\n\r\n");
        }

        var payload = Encoding.UTF8.GetBytes(Body);
        var head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n"
            + $"ETag: {Tag}\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        return [.. head, .. payload];
    }

    private static string FindServerContent()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "server", "content");
            if (File.Exists(Path.Combine(candidate, "announcements.json")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate server/content above '{AppContext.BaseDirectory}'.");
    }
}
