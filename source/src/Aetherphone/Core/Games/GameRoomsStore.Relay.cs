using System.Net;
using System.Runtime.CompilerServices;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;

namespace Aetherphone.Core.Games;

internal sealed partial class GameRoomsStore
{
    internal const string ReasonServerMissing = "server_missing";
    internal const string ReasonInviteShape = "invite_shape";
    internal const string ReasonInviteRejected = "invite";
    internal const string ReasonRelayRefused = "relay_refused";
    internal const string ReasonConnection = "connection";
    internal const string ReasonAlreadyInRoom = "already_hosting";
    internal const long RelayActTimeoutMilliseconds = 8_000;
    internal const long RelayHandshakeTimeoutMilliseconds = 15_000;
    internal const long ClaimCooldownMilliseconds = 2_000;

    private const string ReasonFull = "full";
    private const string ReasonUnavailable = "unavailable";
    private const long ClaimGraceMilliseconds = 500;

    private readonly object relayGate = new();

    private GameRoomIntent? relayIntent;
    private string relayKind = string.Empty;
    private string relayCode = string.Empty;
    private bool handshakePending;
    private long handshakeAtTick;
    private string relayActId = string.Empty;
    private long relayActAtTick;
    private long claimAtTick;
    private GameRoomState? cardedState;
    private StrongBox<RelayFailure>? pendingRelayFailure;

    public bool RelayMode => GameSignalRouter.RelayMode;

    public RelayFailure LastRelayRefusal { get; private set; }

    public bool RelayConfigured => router.Relay.IsConfigured;

    public string ShareInvite(string code)
    {
        if (!RelayMode || code.Length == 0 || !router.Relay.TryGetSessionServer(out var server, out var invite))
        {
            return code;
        }

        return PartyRelayLink.Format(server, invite, code);
    }

    private string AdoptInvite(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.Contains(PartyRelayLink.Separator))
        {
            return PartyCode.Normalize(trimmed);
        }

        if (!PartyRelayLink.TryParse(trimmed, out var link))
        {
            return string.Empty;
        }

        router.Relay.SaveServer(link.Server, link.Invite);
        return link.Code;
    }

    private void CreateRelayRoom(string gameKind)
    {
        if (intentInFlight || gameKind.Length == 0)
        {
            return;
        }

        BeginRelayIntent(GameRoomIntent.Created, null, gameKind);
    }

    private void JoinRelayRoom(string text)
    {
        if (intentInFlight)
        {
            return;
        }

        var code = AdoptInvite(text);
        if (code.Length == 0)
        {
            Answer(GameRoomIntent.Joined, false, ReasonInviteShape, null);
            return;
        }

        BeginRelayIntent(GameRoomIntent.Joined, code, string.Empty);
    }

    private void BeginRelayIntent(GameRoomIntent intent, string? code, string gameKind)
    {
        lock (relayGate)
        {
            if (relayIntent is not null || relayCode.Length > 0)
            {
                Answer(intent, false, ReasonAlreadyInRoom, null);
                return;
            }

            if (!router.BeginRelay(code))
            {
                Answer(intent, false, ReasonServerMissing, null);
                return;
            }

            relayIntent = intent;
            relayKind = gameKind;
            relayCode = code ?? string.Empty;
            handshakePending = true;
            handshakeAtTick = Environment.TickCount64;
            intentInFlight = true;
        }
    }

    private void LeaveRelayRoom(string roomId, GameRoomIntent intent)
    {
        string code;
        lock (relayGate)
        {
            code = relayIntent is null ? relayCode : string.Empty;
        }

        if (code.Length > 0 && string.Equals(code, roomId, StringComparison.Ordinal))
        {
            router.Send(new CallControl
            {
                Type = intent == GameRoomIntent.Closed ? SignalType.GameClose : SignalType.GameLeave,
                Game = new GamePayload { RoomId = code },
            });
        }

        room.Reset();
        EndRelayRoom();
        Answer(intent, true, string.Empty, null);
    }

    private void KickInRelay(string userId)
    {
        if (intentInFlight || userId.Length == 0 || room.RoomId.Length == 0)
        {
            return;
        }

        var sent = router.Send(new CallControl { Type = SignalType.GameKick, UserId = userId });
        Answer(GameRoomIntent.Kicked, sent, sent ? string.Empty : ReasonUnavailable, null);
    }

    private void SendRelayAction(string target, GameRoomActionRequest request)
    {
        lock (relayGate)
        {
            relayActId = request.ClientActionId;
            relayActAtTick = Environment.TickCount64;
        }

        var sent = router.Send(new CallControl
        {
            Type = SignalType.GameAct,
            Game = new GamePayload { RoomId = target, Act = request },
        });
        if (!sent)
        {
            SettleRelayAction(request.ClientActionId, new GameActOutcome(false, string.Empty));
        }
    }

    private void SettleRelayAction(string? clientActionId, GameActOutcome outcome)
    {
        lock (relayGate)
        {
            if (relayActId.Length == 0 || !string.Equals(relayActId, clientActionId, StringComparison.Ordinal))
            {
                return;
            }

            relayActId = string.Empty;
        }

        Interlocked.Exchange(ref actOutcome, outcome);
        actInFlight = false;
    }

    private void OnRelaySignal(GameSignal signal)
    {
        var payload = signal.Payload;
        var now = NowUnixMilliseconds();
        switch (signal.Type)
        {
            case SignalType.GameAttached when payload is { RoomId.Length: > 0 }:
                AdoptRelayRoom(signal, payload.RoomId, now);
                return;
            case SignalType.GameDeclined:
                FailRelay(signal.Reason is { Length: > 0 } reason ? reason : ReasonUnavailable);
                return;
            case SignalType.GameActed:
                var refusal = signal.Reason ?? string.Empty;
                SettleRelayAction(payload?.ClientActionId, new GameActOutcome(refusal.Length == 0, refusal));
                return;
            case SignalType.GameEnded:
                room.Receive(signal, now);
                EndRelayRoom();
                return;
            default:
                room.Receive(signal, now);
                return;
        }
    }

    private void AdoptRelayRoom(GameSignal signal, string code, long now)
    {
        GameRoomIntent? answered;
        lock (relayGate)
        {
            answered = relayIntent;
            if (answered is not null)
            {
                relayIntent = null;
                relayCode = code;
                room.Adopt(code);
                router.Relay.SetCode(code);
                router.Relay.SetRole(answered == GameRoomIntent.Created ? PartyRelayRole.Host : PartyRelayRole.Viewer);
            }
        }

        room.Receive(signal, now);
        if (answered is not { } intent)
        {
            return;
        }

        var card = RelayCard(code);
        lock (relayGate)
        {
            cardedState = room.State;
        }

        intentInFlight = false;
        Answer(intent, true, string.Empty, card);
    }

    private void OnRelayConnected(bool connected)
    {
        if (!connected)
        {
            room.OnRealtimeConnected(false);
            return;
        }

        lock (relayGate)
        {
            if (relayIntent is not null || relayCode.Length > 0)
            {
                handshakePending = true;
            }
        }
    }

    private void OnRelayFailed(RelayFailure failure) =>
        Interlocked.Exchange(ref pendingRelayFailure, new StrongBox<RelayFailure>(failure));

    private void TickRelay()
    {
        var nowTick = Environment.TickCount64;
        ApplyRelayFailure();
        SendRelayHandshake(nowTick);
        ExpireRelayAction(nowTick);
        RefreshRelayCard();
        ClaimOverdueTurn(nowTick);
    }

    private void ApplyRelayFailure()
    {
        if (Interlocked.Exchange(ref pendingRelayFailure, null) is not { } pendingFailure)
        {
            return;
        }

        var failure = pendingFailure.Value;
        if (failure.Refusal != RelayRefusal.None)
        {
            LastRelayRefusal = failure;
            FailRelay(ReasonRelayRefused);
            return;
        }

        bool pending;
        lock (relayGate)
        {
            pending = relayIntent is not null;
        }

        FailRelay(failure.Status switch
        {
            (int)HttpStatusCode.Unauthorized => ReasonInviteRejected,
            (int)HttpStatusCode.NotFound => pending ? ReasonUnavailable : GameRoomWire.ReasonEnded,
            (int)HttpStatusCode.Conflict => ReasonFull,
            _ => ReasonConnection,
        });
    }

    private void SendRelayHandshake(long nowTick)
    {
        GameRoomIntent? intent;
        string code;
        string kind;
        lock (relayGate)
        {
            if (relayIntent is not null && nowTick - handshakeAtTick >= RelayHandshakeTimeoutMilliseconds)
            {
                handshakePending = false;
                intent = null;
                code = string.Empty;
                kind = string.Empty;
            }
            else if (!handshakePending || !router.RelayConnected)
            {
                return;
            }
            else
            {
                handshakePending = false;
                intent = relayIntent;
                code = relayCode;
                kind = relayKind;
            }
        }

        if (intent == GameRoomIntent.Created)
        {
            router.Send(new CallControl { Type = SignalType.GameCreate, Game = new GamePayload { GameKind = kind } });
            return;
        }

        if (code.Length > 0)
        {
            router.Send(new CallControl { Type = SignalType.GameJoin, Code = code });
            return;
        }

        FailRelay(ReasonConnection);
    }

    private void ExpireRelayAction(long nowTick)
    {
        string pending;
        lock (relayGate)
        {
            if (relayActId.Length == 0 || nowTick - relayActAtTick < RelayActTimeoutMilliseconds)
            {
                return;
            }

            pending = relayActId;
        }

        SettleRelayAction(pending, new GameActOutcome(false, string.Empty));
    }

    private void RefreshRelayCard()
    {
        var held = room.State;
        string code;
        lock (relayGate)
        {
            if (relayIntent is not null || relayCode.Length == 0 || held is null || ReferenceEquals(held, cardedState))
            {
                return;
            }

            cardedState = held;
            code = relayCode;
        }

        rooms = [RelayCard(code)];
    }

    private void ClaimOverdueTurn(long nowTick)
    {
        var held = room.State;
        if (held?.Roster is not { } roster || !room.Attached || held.Snapshot.Phase != GameRoomWire.PhasePlaying)
        {
            return;
        }

        var deadline = held.Snapshot.PhaseEndsAtUnixMs;
        if (deadline <= 0 || nowTick - claimAtTick < ClaimCooldownMilliseconds
            || room.ServerNowUnixMs(NowUnixMilliseconds()) < deadline + ClaimGraceMilliseconds
            || !Seated(roster, AccountId))
        {
            return;
        }

        claimAtTick = nowTick;
        router.Send(new CallControl { Type = SignalType.GameClaim, Game = new GamePayload { RoomId = held.RoomId } });
    }

    private static bool Seated(GameRoomRoster roster, string userId)
    {
        var players = roster.Players;
        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return userId.Length > 0;
            }
        }

        return false;
    }

    private GameRoomCardDto RelayCard(string code)
    {
        var held = room.State;
        var roster = held?.Roster;
        var hostId = roster?.HostUserId ?? string.Empty;
        var hostName = string.Empty;
        var seated = 0;
        var players = roster?.Players ?? Array.Empty<GameRoomMemberView>();
        for (var index = 0; index < players.Length; index++)
        {
            if (!players[index].Away)
            {
                seated++;
            }

            if (string.Equals(players[index].UserId, hostId, StringComparison.Ordinal))
            {
                hostName = players[index].DisplayName;
            }
        }

        string kind;
        lock (relayGate)
        {
            kind = held?.Snapshot.GameKind ?? relayKind;
        }

        return new GameRoomCardDto(RoomId: code, GameKind: kind, OwnerUserId: hostId, OwnerName: hostName,
            SeatedCount: seated, Occupancy: held?.Snapshot.Occupancy ?? 0, Phase: held?.Snapshot.Phase ?? 0,
            Member: true, JoinCode: code);
    }

    private void FailRelay(string reason)
    {
        GameRoomIntent? failed;
        string code;
        lock (relayGate)
        {
            failed = relayIntent;
            code = relayCode;
        }

        EndRelayRoom();
        if (failed is { } intent)
        {
            intentInFlight = false;
            Answer(intent, false, reason, null);
            return;
        }

        if (code.Length > 0)
        {
            room.CloseFromHttp(code, reason);
        }
    }

    private void EndRelayRoom()
    {
        bool acting;
        bool pending;
        lock (relayGate)
        {
            pending = relayIntent is not null;
            relayIntent = null;
            relayKind = string.Empty;
            relayCode = string.Empty;
            handshakePending = false;
            acting = relayActId.Length > 0;
            relayActId = string.Empty;
            cardedState = null;
        }

        router.EndRelay();
        rooms = Array.Empty<GameRoomCardDto>();
        if (acting)
        {
            actInFlight = false;
        }

        if (pending)
        {
            intentInFlight = false;
        }
    }
}
