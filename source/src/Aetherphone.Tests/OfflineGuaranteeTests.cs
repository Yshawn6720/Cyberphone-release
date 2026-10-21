using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Aetherphone.Core.Net;
using Aetherphone.Core.Telephony;
using Xunit;

namespace Aetherphone.Tests;

/// <summary>
/// Cyberphone's guarantee that it never talks to the official Aethernet service: the runtime guard refuses
/// the official hosts, and a static sentinel keeps every connection funnelled through that guard.
/// </summary>
public sealed class OfflineGuaranteeTests
{
    private const string GuardFile = "src/Aetherphone/Core/Net/OfficialHostGuard.cs";
    private static readonly string[] ProductSourceRoots = { "src/Aetherphone", "src/ManagedDoom" };
    private static readonly string[] OfficialDomains = { "aetherphone.net", "up.railway.app" };

    private static readonly Regex NetworkConstruction = new(
        @"\bnew\s+(?:global::)?(?:System\.Net\.(?:Http|WebSockets|Sockets)\.)?" +
        @"(?:HttpClient|HttpClientHandler|SocketsHttpHandler|WinHttpHandler|HttpMessageInvoker|ClientWebSocket|TcpClient|WebClient)\s*[({]" +
        @"|\b(?:HttpClient|HttpClientHandler|SocketsHttpHandler|WinHttpHandler|HttpMessageInvoker|ClientWebSocket|TcpClient|WebClient)\??\s+\w+\s*(?:\{[^}]*\}\s*)?(?:=|=>)\s*new\s*\(" +
        @"|\bWebRequest\.Create(?:Http)?\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex OfficialDomain = new(
        string.Join("|", OfficialDomains.Select(Regex.Escape)), RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Theory]
    [InlineData("aetherphone.net")]
    [InlineData("api.aetherphone.net")]
    [InlineData("www.aetherphone.net")]
    [InlineData("media.aetherphone.net")]
    [InlineData("API.AETHERPHONE.NET.")]
    [InlineData("aethernet-dev-production.up.railway.app")]
    [InlineData("bucket-production-d88c.up.railway.app")]
    [InlineData("ffxiv-aethernet-production.up.railway.app")]
    public void OfficialHostsAreBlocked(string host)
    {
        Assert.True(OfficialHostGuard.IsBlocked(host));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notaetherphone.net")]
    [InlineData("aetherphone.net.example.com")]
    [InlineData("example.com")]
    [InlineData("relay.cyberphone.invalid")]
    public void OtherHostsAreAllowed(string? host)
    {
        Assert.False(OfficialHostGuard.IsBlocked(host));
    }

    [Fact]
    public void EveryDomainTheSentinelForbidsIsBlockedAtRuntime()
    {
        foreach (var domain in OfficialDomains)
        {
            Assert.True(OfficialHostGuard.IsBlocked("probe." + domain), domain);
        }
    }

    [Fact]
    public void BlockedUrlCheckIgnoresFilesAndRelativeUrls()
    {
        Assert.True(OfficialHostGuard.IsBlockedUrl("https://media.aetherphone.net/a.mp4"));
        Assert.False(OfficialHostGuard.IsBlockedUrl("file:///C:/videos/a.mp4"));
        Assert.False(OfficialHostGuard.IsBlockedUrl("videos/a.mp4"));
        Assert.Throws<HttpRequestException>(() => OfficialHostGuard.EnsureAllowed(new Uri("wss://api.aetherphone.net/rt")));
    }

    [Fact]
    public async Task GuardedClientRefusesOfficialHostBeforeConnecting()
    {
        using var client = OfficialHostGuard.CreateClient();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://api.aetherphone.net/flags"));

        Assert.Contains("disabled in this build", Flatten(error));
    }

    [Fact]
    public async Task WrappedClientStillEndsInTheGuard()
    {
        using var client = OfficialHostGuard.CreateClient(inner => new PassThroughHandler(inner));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://api.aetherphone.net/me"));

        Assert.Contains("disabled in this build", Flatten(error));
    }

    [Fact]
    public async Task RedirectToOfficialHostIsRefused()
    {
        using var server = new OneShotHttpServer("HTTP/1.1 302 Found\r\nLocation: https://media.aetherphone.net/x\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var client = OfficialHostGuard.CreateClient();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(server.Url));

        Assert.Contains("disabled in this build", Flatten(error));
    }

    [Fact]
    public async Task GuardedClientReachesOrdinaryHosts()
    {
        using var server = new OneShotHttpServer("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
        using var client = OfficialHostGuard.CreateClient();

        var body = await client.GetStringAsync(server.Url);

        Assert.Equal("ok", body);
    }

    [Fact]
    public async Task GuardedWebSocketRefusesOfficialHost()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            OfficialHostGuard.ConnectWebSocketAsync(new Uri("wss://api.aetherphone.net/rt"), null, CancellationToken.None));
    }

    [Theory]
    [InlineData("https://aetherphone.net")]
    [InlineData("https://api.aetherphone.net/")]
    [InlineData("https://API.AETHERPHONE.NET.")]
    [InlineData("https://ffxiv-aethernet-production.up.railway.app")]
    public async Task PartyRelayPointedAtAnOfficialHostIsNeverConfigured(string url)
    {
        var configuration = new Configuration { PartyRelayUrl = url, PartyRelayInvite = "invite" };
        var relay = new PartyRelay(configuration, () => new PartyRelayIdentity("Mog Ri", "Gilgamesh"));

        Assert.False(relay.IsConfigured);
        Assert.False(relay.Begin(null));
        var socketUri = new UriBuilder(url) { Scheme = Uri.UriSchemeWss, Port = -1, Path = "/rt" }.Uri;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            OfficialHostGuard.ConnectWebSocketAsync(socketUri, null, CancellationToken.None));
    }

    [Fact]
    public void ProductCodeCreatesConnectionsOnlyThroughTheGuard()
    {
        var violations = ScanProductSources(NetworkConstruction);

        Assert.True(violations.Count == 0,
            "Network clients must be created by OfficialHostGuard only:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void ProductCodeNamesOfficialDomainsOnlyInTheGuard()
    {
        var violations = ScanProductSources(OfficialDomain);

        Assert.True(violations.Count == 0,
            "Official Aethernet domains may appear only in OfficialHostGuard.cs:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void SentinelPatternsCatchTheFormsTheyGuardAgainst()
    {
        Assert.Matches(NetworkConstruction, "var client = new HttpClient();");
        Assert.Matches(NetworkConstruction, "var client = new System.Net.Http.HttpClient(handler) { Timeout = x };");
        Assert.Matches(NetworkConstruction, "private static readonly HttpClient Shared = new() { Timeout = x };");
        Assert.Matches(NetworkConstruction, "using var ws = new ClientWebSocket();");
        Assert.Matches(NetworkConstruction, "ClientWebSocket? socket = new();");
        Assert.Matches(NetworkConstruction, "var handler = new SocketsHttpHandler { };");
        Assert.Matches(NetworkConstruction, "var request = WebRequest.Create(url);");
        Assert.DoesNotMatch(NetworkConstruction, "client = OfficialHostGuard.CreateClient();");
        Assert.DoesNotMatch(NetworkConstruction, "private readonly HttpClient client;");
        Assert.Matches(OfficialDomain, "\"https://API.Aetherphone.NET/\"");
    }

    private static List<string> ScanProductSources(Regex pattern)
    {
        var root = FindRepoRoot();
        var violations = new List<string>();
        var scanned = 0;
        foreach (var sourceRoot in ProductSourceRoots)
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, sourceRoot), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
                {
                    continue;
                }

                scanned++;
                if (relative == GuardFile)
                {
                    continue;
                }

                var lines = File.ReadAllLines(path);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (pattern.IsMatch(lines[index]))
                    {
                        violations.Add($"{relative}:{index + 1}: {lines[index].Trim()}");
                    }
                }
            }
        }

        Assert.True(scanned > 1000, $"sentinel scanned only {scanned} files; the source roots moved");
        return violations;
    }

    private static string Flatten(Exception error)
    {
        var text = new StringBuilder();
        for (var current = error; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);
        }

        return text.ToString();
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetherphone.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return current.FullName;
    }

    private sealed class PassThroughHandler : DelegatingHandler
    {
        public PassThroughHandler(HttpMessageHandler inner) : base(inner)
        {
        }
    }

    /// <summary>Answers exactly one request on loopback with a canned HTTP/1.1 response.</summary>
    private sealed class OneShotHttpServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Task serving;

        public OneShotHttpServer(string response)
        {
            listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
            serving = ServeAsync(Encoding.ASCII.GetBytes(response));
        }

        public string Url { get; }

        public void Dispose()
        {
            listener.Stop();
            serving.Wait(TimeSpan.FromSeconds(5));
        }

        private async Task ServeAsync(byte[] response)
        {
            using var connection = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            var stream = connection.GetStream();
            var buffer = new byte[4096];
            var received = new StringBuilder();
            while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var read = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                received.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            await stream.WriteAsync(response).ConfigureAwait(false);
        }
    }
}
