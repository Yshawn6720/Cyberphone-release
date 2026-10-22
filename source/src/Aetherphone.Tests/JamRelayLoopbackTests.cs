using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class JamRelayLoopbackTests
{
    private static readonly string[] JamUplinks =
    [
        SignalType.JamStart, SignalType.JamJoin, SignalType.JamLeave, SignalType.JamEnd, SignalType.JamState,
        SignalType.JamControl, SignalType.JamTransfer, SignalType.JamQueueAdd, SignalType.JamQueueAdvance,
        SignalType.JamQueueRemove, SignalType.JamQueueMove, SignalType.JamApprove, SignalType.JamDeny,
        SignalType.JamKick, SignalType.JamReact, SignalType.JamSettings, SignalType.JamNearby,
    ];

    private static readonly string[] JamRefused =
    [
        SignalType.JamInvite, SignalType.JamChat, SignalType.JamDeleteMessage, SignalType.StreamState,
    ];

    private static JamSignalRouter CreateRouter(string relayUrl)
    {
        var configuration = new Configuration
        {
            PartyRelayUrl = relayUrl,
            PartyRelayInvite = PartyRelayLoopbackTests.Invite,
        };
        var identity = () => (PartyRelayIdentity?)new PartyRelayIdentity(PartyRelayLoopbackTests.Name,
            PartyRelayLoopbackTests.World);
        var session = new AethernetSession(configuration, null!);
        var calls = new CallSignalRouter(session, new RealtimeSignalBus(), new PartyRelay(configuration, identity));
        return new JamSignalRouter(calls, new PartyRelay(configuration, identity, PartyRelayKind.Jam));
    }

    [Fact]
    public async Task ConfiguredJamRelayOpensNoConnectionUntilAJamBegins()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        Assert.True(router.Relay.RelayReady);
        Assert.False(router.RelayActive);
        Assert.False(router.Send(new CallControl { Type = SignalType.JamState }));
        Assert.Equal(0, server.ConnectionCount);
    }

    [Fact]
    public async Task StartingAJamAsksTheRelayForAJamRoom()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        Assert.True(router.BeginRelay(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));
        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /rt?kind=jam&create=1&", LoopbackRelayServer.RequestLine(request),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task JoiningFromTheNearbyListAsksWithTheNearbyCode()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        Assert.True(router.BeginRelay("ABC234", nearby: true));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));
        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /rt?kind=jam&nearby=ABC234&", LoopbackRelayServer.RequestLine(request),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task BegunJamRelayCarriesOnlyTheJamUplinks()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        Assert.True(router.BeginRelay("ABC123"));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));

        foreach (var type in JamRefused)
        {
            Assert.False(router.Send(new CallControl { Type = type }), type);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(0, server.ReceivedBytes);

        foreach (var type in JamUplinks)
        {
            Assert.True(router.Send(new CallControl { Type = type }), type);
        }

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedBytes > 0,
            PartyRelayLoopbackTests.Patience));
    }

    [Fact]
    public async Task EndingTheJamClosesTheRelayAndStaysClosed()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));

        router.EndRelay();

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ClosedCount == 1,
            PartyRelayLoopbackTests.Patience));
        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.False(router.Connected);
        Assert.Equal(1, server.ConnectionCount);
    }
}
