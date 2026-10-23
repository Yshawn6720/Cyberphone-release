using System.Numerics;
using System.Text.Json;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PartyRelayLoopbackTests
{
    internal const string Invite = "loopback-invite";
    internal const string Name = "莫古 Mog";
    internal const string World = "紫水栈桥";
    // RFC 6455 section 5.2: a client frame is 2 header bytes, an extended length past 125, then a 4 byte mask.
    private const int FrameHeaderBytes = 2;
    private const int MaskBytes = 4;
    private const int LongestShortPayload = 125;
    private const int ReactionTaps = 20;
    private const int DanmakuSends = 12;
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReconnectWindow = TimeSpan.FromSeconds(5.5);
    private const string RateLimitedRefusal =
        "{\"type\":\"relay.refused\",\"reason\":\"rate_limited\",\"retryAfterSeconds\":60}";
    private const ushort RateLimitedClose = 4429;

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
    public async Task JoiningFromTheNearbyListAsksWithTheNearbyCode()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        Assert.True(router.BeginRelay("ABC234", nearby: true));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));
        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /rt?nearby=ABC234&", LoopbackRelayServer.RequestLine(request), StringComparison.Ordinal);
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
        var failed = new TaskCompletionSource<RelayFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.RelayFailed += failure => failed.TrySetResult(failure);

        Assert.True(router.BeginRelay("ABC123"));

        var finished = await Task.WhenAny(failed.Task, Task.Delay(Patience));
        Assert.Same(failed.Task, finished);
        var failure = await failed.Task;
        Assert.Equal(401, failure.Status);
        Assert.Equal(RelayRefusal.None, failure.Refusal);
        await Task.Delay(ReconnectWindow);
        Assert.Equal(1, server.ConnectionCount);
        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /rt?code=ABC123&", LoopbackRelayServer.RequestLine(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AViewerTheRelayRateLimitsFailsOnceWithTheWaitAndNeverReconnects()
    {
        using var server = new LoopbackRelayServer(farewell: RateLimitedRefusal, farewellCloseCode: RateLimitedClose);
        using var router = CreateRouter(server.Url);
        var failures = 0;
        var failed = new TaskCompletionSource<RelayFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.RelayFailed += failure =>
        {
            Interlocked.Increment(ref failures);
            failed.TrySetResult(failure);
        };

        Assert.True(router.BeginRelay("ABC123"));

        var finished = await Task.WhenAny(failed.Task, Task.Delay(Patience));
        Assert.Same(failed.Task, finished);
        var failure = await failed.Task;
        Assert.Equal(RelayRefusal.RateLimited, failure.Refusal);
        Assert.Equal(60, failure.RetryAfterSeconds);
        await Task.Delay(ReconnectWindow);
        Assert.Equal(1, server.ConnectionCount);
        Assert.Equal(1, Volatile.Read(ref failures));
    }

    [Fact]
    public async Task AViewerWhoseRelayDropsRightAfterRefusingStillFailsOnceAndNeverReconnects()
    {
        using var server = new LoopbackRelayServer(farewell: RateLimitedRefusal, abortAfterFarewell: true);
        using var router = CreateRouter(server.Url);
        var failures = 0;
        var failed = new TaskCompletionSource<RelayFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.RelayFailed += failure =>
        {
            Interlocked.Increment(ref failures);
            failed.TrySetResult(failure);
        };

        Assert.True(router.BeginRelay("ABC123"));

        var finished = await Task.WhenAny(failed.Task, Task.Delay(Patience));
        Assert.Same(failed.Task, finished);
        var failure = await failed.Task;
        Assert.Equal(RelayRefusal.RateLimited, failure.Refusal);
        Assert.Equal(60, failure.RetryAfterSeconds);
        await Task.Delay(ReconnectWindow);
        Assert.Equal(1, server.ConnectionCount);
        Assert.Equal(1, Volatile.Read(ref failures));
    }

    [Fact]
    public async Task AnOlderRelayThatRefusesWithHttp503StillReportsRoomsFull()
    {
        using var server = new LoopbackRelayServer(503);
        using var router = CreateRouter(server.Url);
        var failed = new TaskCompletionSource<RelayFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.RelayFailed += failure => failed.TrySetResult(failure);

        Assert.True(router.BeginRelay("ABC123"));

        var finished = await Task.WhenAny(failed.Task, Task.Delay(Patience));
        Assert.Same(failed.Task, finished);
        var failure = await failed.Task;
        Assert.Equal(503, failure.Status);
        Assert.Equal(RelayRefusal.RoomsFull, failure.Refusal);
    }

    [Fact]
    public async Task ReactionsBeyondTheBudgetNeverLeaveTheClient()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));
        var reaction = new CallControl { Type = SignalType.StreamReact, Reaction = 0 };
        var payload = JsonSerializer.SerializeToUtf8Bytes(reaction, TelephonyJsonContext.Default.CallControl);

        var accepted = 0;
        for (var index = 0; index < ReactionTaps; index++)
        {
            if (router.Send(reaction))
            {
                accepted++;
            }
        }

        var expectedBytes = accepted * MaskedFrameBytes(payload.Length);
        Assert.Equal(CallSignalRouter.ReactionBurst, accepted);
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedBytes >= expectedBytes, Patience));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(expectedBytes, server.ReceivedBytes);
    }

    [Fact]
    public async Task DanmakuBeyondTheBudgetNeverLeavesTheClient()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));
        var line = new CallControl { Type = SignalType.StreamDanmaku, Text = "hello" };
        var payload = JsonSerializer.SerializeToUtf8Bytes(line, TelephonyJsonContext.Default.CallControl);

        var accepted = 0;
        for (var index = 0; index < DanmakuSends; index++)
        {
            if (router.Send(line))
            {
                accepted++;
            }
        }

        var expectedBytes = accepted * MaskedFrameBytes(payload.Length);
        Assert.Equal(CallSignalRouter.DanmakuBurst, accepted);
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedBytes >= expectedBytes, Patience));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(expectedBytes, server.ReceivedBytes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheHostStateCarriesTheNearbyOptInAndSaysNearbyFollowsApproval(bool discoverable)
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        using var stream = new StreamSignalRouter(router);
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));

        stream.PublishState(new StreamPublication("https://video.test/a", 1, false, 132, 73, false, discoverable,
            false, 0, false, null, null, null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedTexts.Length > 0, Patience));
        using var sent = JsonDocument.Parse(server.ReceivedTexts[0]);
        Assert.Equal("stream.state", sent.RootElement.GetProperty("type").GetString());
        Assert.Equal(discoverable, sent.RootElement.GetProperty("discoverable").GetBoolean());
        Assert.Equal(discoverable, sent.RootElement.GetProperty("nearbyOptIn").GetBoolean());
        Assert.True(sent.RootElement.GetProperty("nearbyFollowsApproval").GetBoolean());
        Assert.False(sent.RootElement.GetProperty("danmakuEnabled").GetBoolean());
    }

    [Fact]
    public async Task TheHostStateCarriesScreenBAndThePlayModeBesideScreenA()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        using var stream = new StreamSignalRouter(router);
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));
        var screenB = new StreamScreenPose(new Vector3(1f, 2f, 3f), 0.5f, 0.1f, 0.2f, 1.5f, 0.25f);

        stream.PublishState(new StreamPublication("https://video.test/a", 1, false, 132, 73, false, false, false, 0,
            false, null, null, null, new StreamChannelState("https://video.test/b", 42.5, true, screenB), "b"));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedTexts.Length > 0, Patience));
        using var sent = JsonDocument.Parse(server.ReceivedTexts[0]);
        var root = sent.RootElement;
        Assert.Equal("https://video.test/a", root.GetProperty("url").GetString());
        Assert.Equal("https://video.test/b", root.GetProperty("urlB").GetString());
        Assert.Equal(42.5, root.GetProperty("positionSecondsB").GetDouble());
        Assert.True(root.GetProperty("pausedB").GetBoolean());
        Assert.Equal(1f, root.GetProperty("screenXB").GetSingle());
        Assert.Equal(2f, root.GetProperty("screenYB").GetSingle());
        Assert.Equal(3f, root.GetProperty("screenZB").GetSingle());
        Assert.Equal(0.5f, root.GetProperty("screenYawB").GetSingle());
        Assert.Equal(0.1f, root.GetProperty("screenPitchB").GetSingle());
        Assert.Equal(0.2f, root.GetProperty("screenRollB").GetSingle());
        Assert.Equal(1.5f, root.GetProperty("screenScaleB").GetSingle());
        Assert.Equal(0.25f, root.GetProperty("screenCurveB").GetSingle());
        Assert.Equal("b", root.GetProperty("playMode").GetString());
    }

    [Fact]
    public async Task AStateWithoutScreenBSendsNoScreenBFields()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        using var stream = new StreamSignalRouter(router);
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));

        stream.PublishState(new StreamPublication("https://video.test/a", 1, false, 132, 73, false, false, false, 0,
            false, null, null, null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedTexts.Length > 0, Patience));
        using var sent = JsonDocument.Parse(server.ReceivedTexts[0]);
        foreach (var property in sent.RootElement.EnumerateObject())
        {
            Assert.False(property.Name.EndsWith('B'), property.Name);
            Assert.NotEqual("playMode", property.Name);
        }
    }

    [Fact]
    public async Task ControlsFailuresAndDanmakuForScreenBNameTheirScreen()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        using var stream = new StreamSignalRouter(router);
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));

        stream.Control(StreamControlAction.Pause, null, "b");
        stream.ReportPlaybackFailure("https://video.test/b", "gone", "b");
        Assert.True(stream.SendDanmaku("hello", "b"));
        stream.Control(StreamControlAction.Play);

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedTexts.Length >= 4, Patience));
        var texts = server.ReceivedTexts;
        using var control = JsonDocument.Parse(texts[0]);
        using var failure = JsonDocument.Parse(texts[1]);
        using var danmaku = JsonDocument.Parse(texts[2]);
        using var controlA = JsonDocument.Parse(texts[3]);
        Assert.Equal("b", control.RootElement.GetProperty("channel").GetString());
        Assert.Equal("b", failure.RootElement.GetProperty("channel").GetString());
        Assert.Equal("b", danmaku.RootElement.GetProperty("screen").GetString());
        Assert.False(controlA.RootElement.TryGetProperty("channel", out _));
    }

    private static long MaskedFrameBytes(int payloadLength)
    {
        var lengthBytes = payloadLength <= LongestShortPayload ? 0 : payloadLength <= ushort.MaxValue ? 2 : 8;
        return FrameHeaderBytes + lengthBytes + MaskBytes + payloadLength;
    }

    [Theory]
    [InlineData("{\"type\":\"stream.kicked\",\"hostId\":\"Host@World\",\"userId\":\"Guest@World\"}")]
    [InlineData("{\"type\":\"stream.declined\",\"reason\":\"denied\",\"hostId\":\"Host@World\"}")]
    public async Task AViewerTheHostTurnsAwayNeverReconnects(string farewell)
    {
        using var server = new LoopbackRelayServer(farewell: farewell);
        using var router = CreateRouter(server.Url);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.RelayControlReceived += message => received.TrySetResult(message.Type);

        Assert.True(router.BeginRelay("ABC123"));

        var finished = await Task.WhenAny(received.Task, Task.Delay(Patience));
        Assert.Same(received.Task, finished);
        await Task.Delay(ReconnectWindow);
        Assert.Equal(1, server.ConnectionCount);
        Assert.False(router.RelayConnected);
    }

    [Theory]
    [InlineData("host", 3)]
    [InlineData("hostBeforeCode", 0)]
    [InlineData("jamHost", 0)]
    [InlineData("viewer", 3)]
    [InlineData("promotedViewer", 3)]
    [InlineData("removedHost", 0)]
    public void APartyHostReconnectsOnlyOnceItKnowsItsRoomCode(string scenario, int expected)
    {
        var configuration = new Configuration { PartyRelayUrl = "http://127.0.0.1:9", PartyRelayInvite = Invite };
        var kind = scenario == "jamHost" ? PartyRelayKind.Jam : PartyRelayKind.Party;
        var relay = new PartyRelay(configuration, () => new PartyRelayIdentity(Name, World), kind);
        var joinsByCode = scenario is "viewer" or "promotedViewer";
        Assert.True(relay.Begin(joinsByCode ? "ABC234" : null));
        if (scenario is "host" or "jamHost" or "removedHost")
        {
            relay.SetCode("ABC234");
        }

        if (scenario == "promotedViewer")
        {
            relay.SetRole(PartyRelayRole.Host);
        }

        if (scenario == "removedHost")
        {
            relay.StopReconnecting();
        }

        Assert.Equal(expected, relay.Endpoint.MaxReconnects());
    }

    [Fact]
    public async Task AHostWhoseRelayDropsReconnectsWithItsRoomCode()
    {
        const string greeting = "{\"type\":\"stream.state\",\"code\":\"ABC234\",\"hostId\":\"" + Name + "@" + World + "\"}";
        using var server = new LoopbackRelayServer(greeting: greeting);
        using var router = CreateRouter(server.Url);
        router.RelayControlReceived += message =>
        {
            if (message.Code is { } code)
            {
                router.SetRelayCode(code);
            }
        };
        Assert.True(router.BeginRelay(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));

        Assert.True(router.Send(new CallControl { Type = SignalType.StreamState, Url = "https://video.test/a" }));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.Requests.Length == 2, Patience));
        var requests = server.Requests;
        Assert.StartsWith("GET /rt?create=1&", LoopbackRelayServer.RequestLine(requests[0]), StringComparison.Ordinal);
        Assert.StartsWith("GET /rt?code=ABC234&", LoopbackRelayServer.RequestLine(requests[1]), StringComparison.Ordinal);
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.RelayConnected, Patience));
    }
}
