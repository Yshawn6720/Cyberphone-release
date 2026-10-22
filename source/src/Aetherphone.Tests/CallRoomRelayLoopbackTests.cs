using System.Text.Json;
using Aetherphone.Core;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CallRoomRelayLoopbackTests
{
    private const string GivenCode = "XYZ789";
    private const string RosterGreeting = "{\"type\":\"call.roster\",\"callId\":\"6f1c2a43-8d5e-4b7a-9c10-2e3f4a5b6c7d\","
        + "\"code\":\"" + GivenCode + "\",\"participants\":[]}";
    private static readonly TimeSpan ReconnectWindow = TimeSpan.FromSeconds(5.5);
    private const string RoomsFullRefusal = "{\"type\":\"relay.refused\",\"reason\":\"rooms_full\",\"maxRooms\":8}";
    private const ushort RoomsFullClose = 4503;
    private const string AddressRoomsRefusal = "{\"type\":\"relay.refused\",\"reason\":\"address_rooms\",\"maxRooms\":3}";
    private const ushort AddressRoomsClose = 4409;
    private const string GuestId = "Guest@World";
    private const string OtherId = "Other@World";
    private const string JoinPending = "{\"type\":\"call.joinPending\"}";

    private static CallRoomRouter CreateRouter(string relayUrl)
    {
        var configuration = new Configuration
        {
            PartyRelayUrl = relayUrl,
            PartyRelayInvite = PartyRelayLoopbackTests.Invite,
        };
        var identity = () => (PartyRelayIdentity?)new PartyRelayIdentity(PartyRelayLoopbackTests.Name,
            PartyRelayLoopbackTests.World);
        return new CallRoomRouter(new PartyRelay(configuration, identity, PartyRelayKind.Call));
    }

    [Fact]
    public async Task ConfiguredCallRelayOpensNoConnectionUntilARoomBegins()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        Assert.True(router.Relay.RelayReady);
        Assert.False(router.Active);
        Assert.False(router.Leave());
        Assert.Equal(0, server.ConnectionCount);
    }

    [Fact]
    public async Task StartingARoomAsksTheRelayForACallRoomAndJoinsIt()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        Assert.True(router.Begin(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));
        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /rt?kind=call&create=1&", LoopbackRelayServer.RequestLine(request),
            StringComparison.Ordinal);
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ReceivedBytes > 0,
            PartyRelayLoopbackTests.Patience));
    }

    [Fact]
    public async Task AServerWithCallsTurnedOffRefusesOnceAndIsNeverRetried()
    {
        using var server = new LoopbackRelayServer(403);
        using var router = CreateRouter(server.Url);
        var failed = new TaskCompletionSource<RelayFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.Failed += failure => failed.TrySetResult(failure);

        Assert.True(router.Begin("ABC123"));

        var finished = await Task.WhenAny(failed.Task, Task.Delay(PartyRelayLoopbackTests.Patience));
        Assert.Same(failed.Task, finished);
        var failure = await failed.Task;
        Assert.Equal(403, failure.Status);
        Assert.Equal(RelayRefusal.CallsDisabled, failure.Refusal);
        await Task.Delay(ReconnectWindow);
        Assert.Equal(1, server.ConnectionCount);
        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /rt?kind=call&code=ABC123&", LoopbackRelayServer.RequestLine(request),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACreatorTheRelayHasNoCallRoomForFailsOnceWithTheRoomCap()
    {
        using var server = new LoopbackRelayServer(farewell: RoomsFullRefusal, farewellCloseCode: RoomsFullClose);
        using var router = CreateRouter(server.Url);
        var failures = 0;
        var failed = new TaskCompletionSource<RelayFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.Failed += failure =>
        {
            Interlocked.Increment(ref failures);
            failed.TrySetResult(failure);
        };

        Assert.True(router.Begin(null));

        var finished = await Task.WhenAny(failed.Task, Task.Delay(PartyRelayLoopbackTests.Patience));
        Assert.Same(failed.Task, finished);
        var failure = await failed.Task;
        Assert.Equal(RelayRefusal.RoomsFull, failure.Refusal);
        Assert.Equal(8, failure.MaxRooms);
        await Task.Delay(ReconnectWindow);
        Assert.Equal(1, server.ConnectionCount);
        Assert.Equal(1, Volatile.Read(ref failures));
    }

    [Fact]
    public async Task ACreatorWhoseNetworkAlreadyHostsTooManyRoomsFailsOnceWithTheAddressCap()
    {
        using var server = new LoopbackRelayServer(farewell: AddressRoomsRefusal, farewellCloseCode: AddressRoomsClose);
        using var router = CreateRouter(server.Url);
        var failures = 0;
        var failed = new TaskCompletionSource<RelayFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.Failed += failure =>
        {
            Interlocked.Increment(ref failures);
            failed.TrySetResult(failure);
        };

        Assert.True(router.Begin(null));

        var finished = await Task.WhenAny(failed.Task, Task.Delay(PartyRelayLoopbackTests.Patience));
        Assert.Same(failed.Task, finished);
        var failure = await failed.Task;
        Assert.Equal(RelayRefusal.AddressRooms, failure.Refusal);
        Assert.Equal(3, failure.MaxRooms);
        await Task.Delay(ReconnectWindow);
        Assert.Equal(1, server.ConnectionCount);
        Assert.Equal(1, Volatile.Read(ref failures));
    }

    [Fact]
    public async Task StartingARoomAsksTheRelayToHoldNewcomersForTheCreator()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);

        Assert.True(router.Begin(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => Sent(server, SignalType.Join) is not null,
            PartyRelayLoopbackTests.Patience));
        var join = Sent(server, SignalType.Join);
        Assert.NotNull(join);
        Assert.True(join.ApprovalRequired);
    }

    [Fact]
    public async Task AJoinRequestWaitsForTheCreatorAndLettingTheNewcomerInTellsTheRelay()
    {
        using var server = new LoopbackRelayServer(answers: [JoinRequest(GuestId, "Guest")]);
        using var router = CreateRouter(server.Url);

        Assert.True(router.Begin(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.JoinRequests.Length == 1,
            PartyRelayLoopbackTests.Patience));
        var request = router.JoinRequests[0];
        Assert.Equal(GuestId, request.UserId);
        Assert.Equal("Guest", request.Name);
        Assert.Equal("World", request.World);
        Assert.True(router.Approve(GuestId));
        Assert.True(router.JoinRequests.IsEmpty);
        Assert.False(router.Approve(GuestId));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => Sent(server, SignalType.CallApprove) is not null,
            PartyRelayLoopbackTests.Patience));
        Assert.Equal(GuestId, Sent(server, SignalType.CallApprove)?.UserId);
        Assert.Null(Sent(server, SignalType.CallDeny));
    }

    [Fact]
    public async Task DenyingAJoinRequestTellsTheRelayAndDropsTheRequest()
    {
        using var server = new LoopbackRelayServer(answers: [JoinRequest(GuestId, "Guest")]);
        using var router = CreateRouter(server.Url);

        Assert.True(router.Begin(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.JoinRequests.Length == 1,
            PartyRelayLoopbackTests.Patience));
        Assert.True(router.Deny(GuestId));
        Assert.True(router.JoinRequests.IsEmpty);
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => Sent(server, SignalType.CallDeny) is not null,
            PartyRelayLoopbackTests.Patience));
        Assert.Equal(GuestId, Sent(server, SignalType.CallDeny)?.UserId);
        Assert.Null(Sent(server, SignalType.CallApprove));
    }

    [Fact]
    public async Task RepeatedRequestsShowOnceAndANewcomerWhoGivesUpLeavesTheList()
    {
        var cancelled = "{\"type\":\"call.joinCancelled\",\"userId\":\"" + GuestId + "\"}";
        using var server = new LoopbackRelayServer(answers:
            [JoinRequest(GuestId, "Guest"), JoinRequest(OtherId, "Other"), JoinRequest(OtherId, "Other"), cancelled]);
        using var router = CreateRouter(server.Url);

        Assert.True(router.Begin(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(
            () => router.JoinRequests.Length == 1 && router.JoinRequests[0].UserId == OtherId,
            PartyRelayLoopbackTests.Patience));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(1, router.JoinRequests.Length);
    }

    [Fact]
    public async Task ANewcomerTheRelayHoldsWaitsForTheCreatorUntilTheCallEnds()
    {
        using var server = new LoopbackRelayServer(answers: [JoinPending]);
        using var router = CreateRouter(server.Url);

        Assert.True(router.Begin(GivenCode));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.AwaitingApproval,
            PartyRelayLoopbackTests.Patience));
        Assert.True(router.Connected);
        router.End();
        Assert.False(router.AwaitingApproval);
    }

    [Fact]
    public async Task ANewcomerTheCreatorLetsInStopsWaitingWhenTheRosterArrives()
    {
        using var server = new LoopbackRelayServer(answers: [JoinPending, RosterGreeting]);
        using var router = CreateRouter(server.Url);
        var rostered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitedFirst = false;
        router.Received += message =>
        {
            if (message.Type == SignalType.CallJoinPending)
            {
                waitedFirst = router.AwaitingApproval;
            }

            if (message.Type == SignalType.Roster)
            {
                rostered.TrySetResult(router.AwaitingApproval);
            }
        };

        Assert.True(router.Begin(GivenCode));

        var finished = await Task.WhenAny(rostered.Task, Task.Delay(PartyRelayLoopbackTests.Patience));
        Assert.Same(rostered.Task, finished);
        Assert.True(waitedFirst);
        Assert.False(await rostered.Task);
    }

    private static string JoinRequest(string userId, string name)
    {
        return "{\"type\":\"call.joinRequest\",\"userId\":\"" + userId + "\",\"from\":{\"userId\":\"" + userId
            + "\",\"name\":\"" + name + "\",\"world\":\"World\",\"displayName\":\"" + name
            + "\",\"slot\":-1,\"state\":\"pending\",\"muted\":false}}";
    }

    private static CallControl? Sent(LoopbackRelayServer server, string type)
    {
        foreach (var text in server.ReceivedTexts)
        {
            var control = JsonSerializer.Deserialize(text, TelephonyJsonContext.Default.CallControl);
            if (control is not null && string.Equals(control.Type, type, StringComparison.Ordinal))
            {
                return control;
            }
        }

        return null;
    }

    [Fact]
    public async Task EndingTheCallClosesTheRelayAndStaysClosed()
    {
        using var server = new LoopbackRelayServer();
        using var router = CreateRouter(server.Url);
        Assert.True(router.Begin(null));
        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => router.Connected, PartyRelayLoopbackTests.Patience));

        router.End();

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.ClosedCount == 1,
            PartyRelayLoopbackTests.Patience));
        await Task.Delay(ReconnectWindow);
        Assert.False(router.Active);
        Assert.False(router.Connected);
        Assert.Equal(1, server.ConnectionCount);
    }

    [Fact]
    public async Task ACreatorWhoseLinkDropsRejoinsTheRoomTheRelayGaveIt()
    {
        using var server = new LoopbackRelayServer(greeting: RosterGreeting);
        using var router = CreateRouter(server.Url);

        Assert.True(router.Begin(null));

        Assert.True(await LoopbackRelayServer.WaitUntilAsync(() => server.Requests.Length >= 2,
            PartyRelayLoopbackTests.Patience));
        var requests = server.Requests;
        Assert.StartsWith("GET /rt?kind=call&create=1&", LoopbackRelayServer.RequestLine(requests[0]),
            StringComparison.Ordinal);
        Assert.StartsWith("GET /rt?kind=call&code=" + GivenCode + "&", LoopbackRelayServer.RequestLine(requests[1]),
            StringComparison.Ordinal);
        Assert.Equal(GivenCode, router.Code);
    }
}
