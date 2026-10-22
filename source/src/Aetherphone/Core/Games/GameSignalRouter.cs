using Aetherphone.Core.Apps;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Games;

internal sealed class GameSignalRouter : IDisposable
{
    // Below the relay's game bucket (20 at once, then one every 500 ms), so the server never has to drop a move.
    internal const int RelayUplinkBurst = 12;
    internal const long RelayUplinkRefillMilliseconds = 600;

    private readonly RealtimeSignalBus signals;
    private readonly PartyRelay relay;
    private readonly RealtimeConnection relayConnection;
    private readonly SendBudget uplinkBudget = new(RelayUplinkBurst, RelayUplinkRefillMilliseconds);
    private readonly object budgetGate = new();

    public GameSignalRouter(RealtimeSignalBus signals, PartyRelay relay)
    {
        this.signals = signals;
        this.relay = relay;
        relayConnection = new RealtimeConnection(relay.Endpoint);
        signals.GameReceived += OnAethernetSignal;
        signals.ConnectedChanged += OnAethernetConnected;
        relayConnection.ControlReceived += OnRelayControl;
        relayConnection.ConnectedChanged += OnRelayConnected;
        relayConnection.Failed += OnRelayFailed;
    }

    public event Action<GameSignal>? Received;
    public event Action<bool>? ConnectedChanged;
    public event Action<RelayFailure>? RelayFailed;

    public static bool RelayMode => !HiddenApps.AethernetEnabled;

    public bool Connected => HiddenApps.AethernetEnabled ? signals.RealtimeActive : RelayConnected;

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

    public bool Send(CallControl control)
    {
        if (HiddenApps.AethernetEnabled)
        {
            return signals.TrySend(control);
        }

        if (!IsRelayUplink(control.Type) || !RelayConnected)
        {
            return false;
        }

        lock (budgetGate)
        {
            if (!uplinkBudget.TryTake(Environment.TickCount64))
            {
                return false;
            }
        }

        _ = relayConnection.SendControlAsync(control);
        return true;
    }

    private static bool IsRelayUplink(string type) => type is SignalType.GameCreate or SignalType.GameJoin
        or SignalType.GameAttach or SignalType.GameResync or SignalType.GameAct or SignalType.GameClaim
        or SignalType.GameLeave or SignalType.GameClose or SignalType.GameKick;

    private void OnAethernetSignal(GameSignal signal)
    {
        if (HiddenApps.AethernetEnabled)
        {
            Received?.Invoke(signal);
        }
    }

    private void OnAethernetConnected(bool connected)
    {
        if (HiddenApps.AethernetEnabled)
        {
            ConnectedChanged?.Invoke(connected);
        }
    }

    private void OnRelayControl(CallControl message)
    {
        if (!message.Type.StartsWith(SignalType.GamePrefix, StringComparison.Ordinal))
        {
            return;
        }

        if (message.Type is SignalType.GameEnded or SignalType.GameDeclined)
        {
            relay.StopReconnecting();
        }

        Received?.Invoke(new GameSignal(message.Type, message.Reason, message.Game));
    }

    private void OnRelayConnected(bool connected)
    {
        if (!HiddenApps.AethernetEnabled)
        {
            ConnectedChanged?.Invoke(connected);
        }
    }

    private void OnRelayFailed(RelayFailure failure) => RelayFailed?.Invoke(failure);

    public void Dispose()
    {
        signals.GameReceived -= OnAethernetSignal;
        signals.ConnectedChanged -= OnAethernetConnected;
        relayConnection.ControlReceived -= OnRelayControl;
        relayConnection.ConnectedChanged -= OnRelayConnected;
        relayConnection.Failed -= OnRelayFailed;
        relayConnection.Dispose();
    }
}
