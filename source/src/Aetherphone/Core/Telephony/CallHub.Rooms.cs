using System.Net;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal enum CallRoomStart : byte
{
    Started,
    NotReady,
    InvalidInvite,
    Busy,
}

internal sealed partial class CallHub
{
    // A room nobody else joins is left after the same half hour the relay gives a lone party host.
    private const float RoomAloneSeconds = 30f * 60f;
    private const float RoomHandshakeSeconds = 20f;
    private const string LinkpearlAppId = "messages";
    private const string MessageAppId = "message";
    private const string RoomFullReason = "full";
    private const string RoomDeniedReason = "denied";
    private const string RoomHostAwayReason = "host_away";

    public static bool RelayMode => !HiddenApps.AethernetEnabled;

    public static string ScreenAppId => RelayMode ? LinkpearlAppId : MessageAppId;

    public bool RelayConfigured => rooms.Relay.IsConfigured;

    public bool RelayReady => rooms.Relay.RelayReady;

    public string RoomCode => rooms.Code;

    public string ShareInvite() => rooms.ShareInvite();

    public CallRoomStart StartRoom() => OpenRoom(null);

    public CallRoomStart JoinRoom(string text)
    {
        var code = rooms.AdoptInvite(text.Trim());
        return code.Length == 0 ? CallRoomStart.InvalidInvite : OpenRoom(code);
    }

    private CallRoomStart OpenRoom(string? code)
    {
        if (!RelayMode)
        {
            return CallRoomStart.NotReady;
        }

        lock (gate)
        {
            if (state != CallState.Idle)
            {
                return CallRoomStart.Busy;
            }

            state = CallState.Connecting;
            callId = default;
            roster = Array.Empty<ParticipantInfo>();
            incomingFrom = null;
            dialingTo = null;
            connectionLostTicks = 0;
            stateTimer = 0f;
        }

        if (rooms.Begin(code))
        {
            AepLog.Info($"[calls] room-open joining={code is not null}");
            return CallRoomStart.Started;
        }

        CallSession? toDispose;
        lock (gate)
        {
            toDispose = ClearLocked();
        }

        toDispose?.Dispose();
        return CallRoomStart.NotReady;
    }

    private void HandleRoomControl(CallControl message)
    {
        switch (message.Type)
        {
            case SignalType.Roster:
                HandleRoomRoster(message);
                return;
            case SignalType.Declined:
                EndCall(message.Reason switch
                {
                    RoomFullReason => CallEndReason.RoomFull,
                    RoomDeniedReason => CallEndReason.Denied,
                    RoomHostAwayReason => CallEndReason.HostAway,
                    _ => CallEndReason.RoomNotFound,
                });
                return;
            case SignalType.Ended:
                EndCall(CallEndReason.RoomClosed);
                return;
        }
    }

    private void HandleRoomRoster(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        var participants = message.Participants ?? Array.Empty<ParticipantInfo>();
        var localId = LocalUserId;
        CallSession? sessionToDispose = null;
        var becameActive = false;
        lock (gate)
        {
            if (state == CallState.Idle)
            {
                return;
            }

            callId = id;
            roster = participants;
            connectionLostTicks = 0;
            var localSlot = -1;
            var others = 0;
            for (var index = 0; index < participants.Length; index++)
            {
                var participant = participants[index];
                if (participant.UserId == localId)
                {
                    localSlot = participant.Slot;
                }
                else if (participant.State == ParticipantState.Active)
                {
                    others++;
                }
            }

            if (others > 0)
            {
                audio.EnsureStartedLocked(id, localSlot);
                audio.SyncRemotesLocked(participants, localId);
                becameActive = state != CallState.Active;
                state = CallState.Active;
            }
            else
            {
                if (audio.HasSessionLocked)
                {
                    sessionToDispose = audio.TakeLocked();
                }

                if (state != CallState.Dialing)
                {
                    stateTimer = 0f;
                }

                state = CallState.Dialing;
            }
        }

        sessionToDispose?.Dispose();
        if (becameActive)
        {
            UiFeedback.Play(UiSound.CallConnect);
        }
    }

    private void OnRoomConnectedChanged(bool connected)
    {
        if (connected)
        {
            return;
        }

        lock (gate)
        {
            if (state != CallState.Idle && connectionLostTicks == 0)
            {
                connectionLostTicks = Environment.TickCount64;
            }
        }
    }

    private void OnRoomFailed(RelayFailure failure)
    {
        bool joined;
        lock (gate)
        {
            if (state == CallState.Idle)
            {
                return;
            }

            joined = state is CallState.Dialing or CallState.Active;
        }

        if (failure.Refusal != RelayRefusal.None)
        {
            EndCall(CallEndReason.Refused, failure);
            return;
        }

        EndCall(failure.Status switch
        {
            (int)HttpStatusCode.NotFound => CallEndReason.RoomNotFound,
            (int)HttpStatusCode.Conflict => CallEndReason.RoomsBusy,
            _ => joined ? CallEndReason.ConnectionLost : CallEndReason.Unreachable,
        });
    }

    private void AdvanceRoom(float deltaSeconds)
    {
        var ending = CallEndReason.None;
        lock (gate)
        {
            if (state == CallState.Idle)
            {
                return;
            }

            if (connectionLostTicks != 0 && Environment.TickCount64 - connectionLostTicks >= ReconnectGraceMs)
            {
                ending = CallEndReason.ConnectionLost;
            }
            else if (state == CallState.Connecting && rooms.AwaitingApproval)
            {
                stateTimer = 0f;
            }
            else if (state == CallState.Connecting)
            {
                stateTimer += deltaSeconds;
                if (stateTimer >= RoomHandshakeSeconds)
                {
                    ending = CallEndReason.Unreachable;
                }
            }
            else if (state == CallState.Dialing)
            {
                stateTimer += deltaSeconds;
                if (stateTimer >= RoomAloneSeconds)
                {
                    ending = CallEndReason.RoomAlone;
                }
            }
        }

        if (ending == CallEndReason.RoomAlone)
        {
            Leave(ending);
        }
        else if (ending != CallEndReason.None)
        {
            EndCall(ending);
        }
    }
}
