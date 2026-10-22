using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OfflineRealtimeTests
{
    private static readonly string[] RelayUplinks =
    [
        SignalType.StreamState, SignalType.StreamJoin, SignalType.StreamLeave, SignalType.StreamControl,
        SignalType.StreamTransfer, SignalType.StreamApprove, SignalType.StreamDeny, SignalType.StreamKick,
        SignalType.StreamReact, SignalType.StreamQueueSuggest, SignalType.StreamQueueApprove,
        SignalType.StreamQueueDeny, SignalType.StreamPlaybackFailed,
    ];

    private static readonly string[] RelayRefused =
    [
        SignalType.JamNearby, SignalType.Invite, SignalType.StreamNearby,
    ];

    [Theory]
    [InlineData(SignalType.StreamState)]
    [InlineData(SignalType.StreamNearby)]
    [InlineData(SignalType.StreamReact)]
    [InlineData(SignalType.StreamKick)]
    [InlineData(SignalType.JamNearby)]
    [InlineData(SignalType.Invite)]
    public void NoRealtimeSendLeavesTheRouterWhileAethernetIsOff(string type)
    {
        var configuration = new Configuration();
        var session = new AethernetSession(configuration, null!);
        var relay = new PartyRelay(configuration,
            () => new PartyRelayIdentity(PartyRelayLoopbackTests.Name, PartyRelayLoopbackTests.World));
        using var router = new CallSignalRouter(session, new RealtimeSignalBus(), relay);

        Assert.False(router.Send(new CallControl { Type = type }));
    }

    [Theory]
    [InlineData(SignalType.StreamState)]
    [InlineData(SignalType.StreamJoin)]
    [InlineData(SignalType.StreamLeave)]
    [InlineData(SignalType.StreamControl)]
    [InlineData(SignalType.StreamTransfer)]
    [InlineData(SignalType.StreamApprove)]
    [InlineData(SignalType.StreamKick)]
    [InlineData(SignalType.StreamReact)]
    [InlineData(SignalType.StreamQueueSuggest)]
    public void ConfiguredRelaySendsNothingBeforeAPartyBegins(string type)
    {
        using var server = new LoopbackRelayServer();
        using var router = PartyRelayLoopbackTests.CreateRouter(server.Url);

        Assert.True(router.Relay.IsConfigured);
        Assert.False(router.Send(new CallControl { Type = type }));
    }

    [Fact]
    public async Task BegunRelayCarriesOnlyTheWhitelistedStreamUplinks()
    {
        using var server = new LoopbackRelayServer();
        using var router = PartyRelayLoopbackTests.CreateRouter(server.Url);

        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected,
            PartyRelayLoopbackTests.Patience));

        foreach (var type in RelayRefused)
        {
            Assert.False(router.Send(new CallControl { Type = type }), type);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(0, server.ReceivedBytes);

        foreach (var type in RelayUplinks)
        {
            Assert.True(router.Send(new CallControl { Type = type }), type);
        }

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedBytes > 0,
            PartyRelayLoopbackTests.Patience));
    }
}
