using Aetherphone.Core.Apps;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Jam;

internal sealed class JamSignalRouter : IDisposable
{
    private readonly CallSignalRouter calls;
    private readonly PartyRelay relay;
    private readonly RealtimeConnection relayConnection;

    public JamSignalRouter(CallSignalRouter calls, PartyRelay relay)
    {
        this.calls = calls;
        this.relay = relay;
        relayConnection = new RealtimeConnection(relay.Endpoint);
        calls.Connection.ControlReceived += OnControl;
        calls.ConnectedChanged += OnConnectedChanged;
        relayConnection.ControlReceived += OnControl;
        relayConnection.ConnectedChanged += OnRelayConnectedChanged;
        relayConnection.Failed += OnRelayFailed;
    }

    public event Action<CallControl>? Received;
    public event Action<bool>? ConnectedChanged;
    public event Action<RelayFailure>? RelayFailed;

    public bool Connected => HiddenApps.AethernetEnabled ? calls.Connected : RelayConnected;

    public bool RelayActive => relay.Active;

    public bool RelayConnected => relay.Active && relayConnection.Connected;

    public PartyRelay Relay => relay;

    public bool BeginRelay(string? code)
    {
        relayConnection.Stop();
        if (!relay.Begin(code))
        {
            relay.End();
            return false;
        }

        relayConnection.Start();
        return true;
    }

    public void EndRelay()
    {
        if (!relay.Active)
        {
            return;
        }

        relay.End();
        relayConnection.Stop();
    }

    public void Start(string? title) => Send(new CallControl { Type = SignalType.JamStart, Title = title });

    public void Join(string code) => Send(new CallControl { Type = SignalType.JamJoin, Code = code });

    public void Leave() => Send(new CallControl { Type = SignalType.JamLeave });

    public void End() => Send(new CallControl { Type = SignalType.JamEnd });

    public void Kick(string userId) => Send(new CallControl { Type = SignalType.JamKick, UserId = userId });

    public void Transfer(string userId) => Send(new CallControl { Type = SignalType.JamTransfer, UserId = userId });

    public void Approve(string userId) => Send(new CallControl { Type = SignalType.JamApprove, UserId = userId });

    public void Deny(string userId) => Send(new CallControl { Type = SignalType.JamDeny, UserId = userId });

    public void Invite(string userId) => Send(new CallControl { Type = SignalType.JamInvite, UserId = userId });

    public void React(int reaction) => Send(new CallControl { Type = SignalType.JamReact, Reaction = reaction });

    public void Chat(string text) => Send(new CallControl { Type = SignalType.JamChat, Text = text });

    public void DeleteMessage(long messageId)
    {
        Send(new CallControl { Type = SignalType.JamDeleteMessage, MessageId = messageId });
    }

    public void Nearby(uint territoryId, uint worldId)
    {
        Send(new CallControl { Type = SignalType.JamNearby, TerritoryId = territoryId, WorldId = worldId });
    }

    public void Settings(int? guestPermissions, bool? approvalRequired, string? title, bool? discoverable = null)
    {
        Send(new CallControl
        {
            Type = SignalType.JamSettings,
            GuestPermissions = guestPermissions,
            ApprovalRequired = approvalRequired,
            Title = title,
            Discoverable = discoverable,
        });
    }

    public void State(JamTrack? track, double positionSeconds, bool paused)
    {
        Send(new CallControl
        {
            Type = SignalType.JamState,
            Track = track,
            PositionSeconds = Math.Max(0d, positionSeconds),
            Paused = paused,
        });
    }

    public void Control(string action, double? positionSeconds)
    {
        Send(new CallControl { Type = SignalType.JamControl, Action = action, PositionSeconds = positionSeconds });
    }

    public bool Send(CallControl control)
    {
        if (HiddenApps.AethernetEnabled)
        {
            return calls.Send(control);
        }

        if (!IsRelayUplink(control.Type) || !RelayConnected)
        {
            return false;
        }

        _ = relayConnection.SendControlAsync(control);
        return true;
    }

    private static bool IsRelayUplink(string type) => type is SignalType.JamStart or SignalType.JamJoin
        or SignalType.JamLeave or SignalType.JamEnd or SignalType.JamState or SignalType.JamControl
        or SignalType.JamTransfer or SignalType.JamQueueAdd or SignalType.JamQueueAdvance
        or SignalType.JamQueueRemove or SignalType.JamQueueMove;

    private void OnControl(CallControl message)
    {
        if (message.Type.StartsWith(SignalType.JamPrefix, StringComparison.Ordinal))
        {
            Received?.Invoke(message);
        }
    }

    private void OnConnectedChanged(bool connected)
    {
        if (HiddenApps.AethernetEnabled)
        {
            ConnectedChanged?.Invoke(connected);
        }
    }

    private void OnRelayConnectedChanged(bool connected)
    {
        if (!HiddenApps.AethernetEnabled)
        {
            ConnectedChanged?.Invoke(connected);
        }
    }

    private void OnRelayFailed(RelayFailure failure) => RelayFailed?.Invoke(failure);

    public void Dispose()
    {
        calls.Connection.ControlReceived -= OnControl;
        calls.ConnectedChanged -= OnConnectedChanged;
        relayConnection.ControlReceived -= OnControl;
        relayConnection.ConnectedChanged -= OnRelayConnectedChanged;
        relayConnection.Failed -= OnRelayFailed;
        relayConnection.Dispose();
    }
}
