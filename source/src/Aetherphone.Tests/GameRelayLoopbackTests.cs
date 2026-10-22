using Aetherphone.Core;
using Aetherphone.Core.Games;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GameRelayLoopbackTests
{
    private const int ServerGameBurst = 20;
    private const int Flood = 50;
    private const string EndedFrame = "{\"type\":\"game.ended\",\"reason\":\"ended\",\"game\":{\"roomId\":\"ABC123\"}}";

    private static readonly string[] GameUplinks =
    [
        SignalType.GameCreate, SignalType.GameJoin, SignalType.GameAttach, SignalType.GameResync, SignalType.GameAct,
        SignalType.GameClaim, SignalType.GameLeave, SignalType.GameClose, SignalType.GameKick,
    ];

    private static readonly string[] GameRefused =
    [
        SignalType.GameMotion, SignalType.GameDetach, SignalType.GameHandled, SignalType.JamState,
        SignalType.StreamState,
    ];

    private static GameSignalRouter CreateRouter(string relayUrl)
    {
        var configuration = new Configuration
        {
            PartyRelayUrl = relayUrl,
            PartyRelayInvite = PartyRelayLoopbackTests.Invite,
        };
        var identity = () => (PartyRelayIdentity?)new PartyRelayIdentity(PartyRelayLoopbackTests.Name,
            PartyRelayLoopbackTests.World);
        return new GameSignalRouter(new RealtimeSignalBus(),
            new PartyRelay(configuration, identity, PartyRelayKind.Game));
    }

    [Fact]
    public async Task ConfiguredGameRelayOpensNoConnectionUntilARoomBegins()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        Assert.True(router.Relay.RelayReady);
        Assert.False(router.RelayActive);
        Assert.False(router.Send(new CallControl { Type = SignalType.GameAct }));
        Assert.Equal(0, server.ConnectionCount);
    }

    [Fact]
    public async Task HostingAGameAsksTheRelayForAGameRoom()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        Assert.True(router.BeginRelay(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));
        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /rt?kind=game&create=1&", LoopbackRelayServer.RequestLine(request),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task JoiningAGameNamesTheRoomCode()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        Assert.True(router.BeginRelay("ABC123"));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));
        Assert.StartsWith("GET /rt?kind=game&code=ABC123&", LoopbackRelayServer.RequestLine(server.Requests[0]),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task BegunGameRelayCarriesOnlyTheGameUplinks()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        Assert.True(router.BeginRelay("ABC123"));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));

        foreach (var type in GameRefused)
        {
            Assert.False(router.Send(new CallControl { Type = type }), type);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(0, server.ReceivedBytes);

        foreach (var type in GameUplinks)
        {
            Assert.True(router.Send(new CallControl { Type = type }), type);
        }

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedBytes > 0,
            PartyRelayLoopbackTests.Patience));
    }

    [Fact]
    public async Task AFloodOfMovesStaysUnderTheRelayGameBucket()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        Assert.True(router.BeginRelay("ABC123"));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));

        var accepted = 0;
        for (var index = 0; index < Flood; index++)
        {
            if (router.Send(new CallControl { Type = SignalType.GameAct }))
            {
                accepted++;
            }
        }

        Assert.InRange(accepted, 1, ServerGameBurst - 1);
    }

    [Fact]
    public async Task ARelayedGameEndReachesTheListener()
    {
        using var server = new LoopbackRelayServer(farewell: EndedFrame);
        using var router = CreateRouter(server.Url);
        GameSignal? heard = null;
        router.Received += signal => heard = signal;

        Assert.True(router.BeginRelay("ABC123"));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => heard is not null, PartyRelayLoopbackTests.Patience));
        var ended = heard!.Value;
        Assert.Equal(SignalType.GameEnded, ended.Type);
        Assert.Equal("ABC123", ended.Payload?.RoomId);
    }

    [Fact]
    public async Task EndingTheGameClosesTheRelayAndStaysClosed()
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
