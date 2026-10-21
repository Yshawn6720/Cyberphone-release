using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Telephony;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PartyRelayLoopbackTests
{
    internal const string Invite = "loopback-invite";
    internal const string Name = "莫古 Mog";
    internal const string World = "紫水栈桥";
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReconnectWindow = TimeSpan.FromSeconds(5.5);

    internal static CallSignalRouter CreateRouter(string relayUrl)
    {
        var configuration = new Configuration { PartyRelayUrl = relayUrl, PartyRelayInvite = Invite };
        var relay = new PartyRelay(configuration, () => new PartyRelayIdentity(Name, World));
        return new CallSignalRouter(new AethernetSession(configuration, null!), new RealtimeSignalBus(), relay);
    }

    [Fact]
    public async Task ConfiguredRelayOpensNoConnectionUntilAPartyBegins()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        Assert.True(router.Relay.IsConfigured);
        Assert.False(router.RelayActive);
        Assert.Equal(0, server.ConnectionCount);
    }

    [Fact]
    public async Task CreatingAPartyOpensOneAuthorizedConnection()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        Assert.True(router.BeginRelay(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));
        Assert.Equal(1, server.ConnectionCount);
        var request = Assert.Single(server.Requests);
        var requestLine = LoopbackRelayServer.RequestLine(request);
        Assert.StartsWith("GET /rt?create=1&", requestLine, StringComparison.Ordinal);
        Assert.Contains("&name=" + Uri.EscapeDataString(Name), requestLine, StringComparison.Ordinal);
        Assert.Contains("&world=" + Uri.EscapeDataString(World), requestLine, StringComparison.Ordinal);
        Assert.Equal("Bearer " + Invite, LoopbackRelayServer.HeaderValue(request, "Authorization"));
        Assert.Equal(Name + "@" + World, router.Relay.SelfId);
    }

    [Fact]
    public async Task EndingTheRelayClosesTheSocketAndNeverReconnects()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));

        router.EndRelay();

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ClosedCount == 1, Patience));
        await Task.Delay(ReconnectWindow);
        Assert.False(router.RelayActive);
        Assert.False(router.RelayConnected);
        Assert.Equal(1, server.ConnectionCount);
    }

    [Fact]
    public async Task RejectedInviteFailsOnceWithoutRetrying()
    {
        using var server = new LoopbackRelayServer(401);
        using var router = CreateRouter(server.Url);
        var failed = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.RelayFailed += status => failed.TrySetResult(status);

        Assert.True(router.BeginRelay("ABC123"));

        var finished = await Task.WhenAny(failed.Task, Task.Delay(Patience));
        Assert.Same(failed.Task, finished);
        Assert.Equal(401, await failed.Task);
        await Task.Delay(ReconnectWindow);
        Assert.Equal(1, server.ConnectionCount);
        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /rt?code=ABC123&", LoopbackRelayServer.RequestLine(request), StringComparison.Ordinal);
    }
}
