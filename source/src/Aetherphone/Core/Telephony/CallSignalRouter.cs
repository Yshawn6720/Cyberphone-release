using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal sealed class CallSignalRouter : IDisposable
{
    // Below the relay's reaction bucket (6 at once, then 1 a second), so network jitter never gets a reaction dropped.
    internal const int ReactionBurst = 4;
    internal const long ReactionRefillMilliseconds = 1000;
    // Below the relay's danmaku bucket (4 at once, then 1 every 1.5 seconds), for the same reason.
    internal const int DanmakuBurst = 3;
    internal const long DanmakuRefillMilliseconds = 1750;

    private readonly RealtimeConnection connection;
    private readonly RealtimeConnection relayConnection;
    private readonly PartyRelay relay;
    private readonly RealtimeSignalBus signals;
    private readonly SendBudget reactionBudget = new(ReactionBurst, ReactionRefillMilliseconds);
    private readonly SendBudget danmakuBudget = new(DanmakuBurst, DanmakuRefillMilliseconds);

    public CallSignalRouter(AethernetSession session, RealtimeSignalBus signals, PartyRelay relay)
    {
        this.signals = signals;
        this.relay = relay;
        connection = new RealtimeConnection(RealtimeEndpoint.Aethernet(session));
        relayConnection = new RealtimeConnection(relay.Endpoint);
        connection.ControlReceived += OnControl;
        connection.ConnectedChanged += OnConnected;
        relayConnection.ControlReceived += OnRelayControl;
        relayConnection.ConnectedChanged += OnRelayConnected;
        relayConnection.Failed += OnRelayFailed;
        signals.BindSender(control => Send(control));
    }

    public event Action<Guid, CallControl>? IncomingReceived;
    public event Action<Guid, CallControl>? RosterReceived;
    public event Action<Guid, CallControl>? DeclinedReceived;
    public event Action<Guid, CallControl>? UnavailableReceived;
    public event Action<Guid, CallControl>? EndedReceived;
    public event Action<Guid, CallControl>? HandledElsewhereReceived;
    public event Action<bool>? ConnectedChanged;
    public event Action<CallControl>? RelayControlReceived;
    public event Action<bool>? StreamConnectedChanged;
    public event Action<RelayFailure>? RelayFailed;

    public bool Connected => connection.Connected;
    public RealtimeConnection Connection => connection;
    public PartyRelay Relay => relay;
    public bool RelayActive => relay.Active;
    public bool RelayConnected => relay.Active && relayConnection.Connected;
    public bool StreamConnected => HiddenApps.AethernetEnabled ? connection.Connected : RelayConnected;

    public void Start() => connection.Start();

    public void Stop() => connection.Stop();

    public bool Send(CallControl control)
    {
        if (HiddenApps.AethernetEnabled)
        {
            _ = connection.SendControlAsync(control);
            return true;
        }

        if (!IsRelayUplink(control.Type) || !relay.IsConfigured || !RelayConnected)
        {
            return false;
        }

        if (control.Type == SignalType.StreamReact && !reactionBudget.TryTake(Environment.TickCount64))
        {
            return false;
        }

        if (control.Type == SignalType.StreamDanmaku && !danmakuBudget.TryTake(Environment.TickCount64))
        {
            return false;
        }

        _ = relayConnection.SendControlAsync(control);
        return true;
    }

    public bool BeginRelay(string? code, bool nearby = false)
    {
        relayConnection.Stop();
        if (!relay.Begin(code, nearby))
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

    public void ReconnectRelay()
    {
        if (!relay.Active)
        {
            return;
        }

        relayConnection.Stop();
        relayConnection.Start();
    }

    public void SetRelayRole(PartyRelayRole role) => relay.SetRole(role);

    public void SetRelayCode(string code) => relay.SetCode(code);

    private static bool IsRelayUplink(string type) => type is SignalType.StreamState or SignalType.StreamJoin
        or SignalType.StreamLeave or SignalType.StreamControl or SignalType.StreamTransfer
        or SignalType.StreamApprove or SignalType.StreamDeny or SignalType.StreamKick or SignalType.StreamReact
        or SignalType.StreamDanmaku or SignalType.StreamQueueSuggest or SignalType.StreamQueueApprove
        or SignalType.StreamQueueDeny or SignalType.StreamPlaybackFailed;

    private void OnConnected(bool isConnected)
    {
        signals.SetActive(isConnected);
        ConnectedChanged?.Invoke(isConnected);
        if (HiddenApps.AethernetEnabled)
        {
            StreamConnectedChanged?.Invoke(isConnected);
        }
    }

    private void OnRelayConnected(bool isConnected)
    {
        if (!HiddenApps.AethernetEnabled)
        {
            StreamConnectedChanged?.Invoke(isConnected);
        }
    }

    private void OnRelayControl(CallControl message)
    {
        if (!message.Type.StartsWith(SignalType.StreamPrefix, StringComparison.Ordinal))
        {
            return;
        }

        if (message.Type is SignalType.StreamKicked or SignalType.StreamDeclined)
        {
            relay.StopReconnecting();
        }

        RelayControlReceived?.Invoke(message);
    }

    private void OnRelayFailed(RelayFailure failure) => RelayFailed?.Invoke(failure);

    private void OnControl(CallControl message)
    {
        if (message.Type.StartsWith(SignalType.CasinoPrefix, StringComparison.Ordinal))
        {
            signals.PublishCasino(new CasinoSignal(message.Type, message.Reason, message.Casino, message.UserId,
                message.Reaction));
            return;
        }

        if (message.Type.StartsWith(SignalType.GamePrefix, StringComparison.Ordinal))
        {
            signals.PublishGame(new GameSignal(message.Type, message.Reason, message.Game));
            return;
        }

        if (message.Type.StartsWith(SignalType.RadioPrefix, StringComparison.Ordinal))
        {
            signals.PublishRadio(message);
            return;
        }

        switch (message.Type)
        {
            case SignalType.ChatPing:
                signals.PublishChat(new ChatSignal(message.ContentId, message.Message));
                return;
            case SignalType.KeysStale:
                signals.PublishKeysStale();
                return;
            case SignalType.KeysLinkPending:
                signals.PublishDeviceLinkRequested();
                return;
            case SignalType.VelvetPing:
                signals.PublishVelvet();
                return;
            case SignalType.GramPing:
                signals.PublishGram();
                return;
            case SignalType.AdPing:
                signals.PublishAds();
                return;
            case SignalType.SocialPing:
                signals.PublishSocial(new SocialSignal(message.App, message.ContentKind));
                return;
            case SignalType.ChatTyping:
            case SignalType.VelvetTyping:
            case SignalType.GramTyping:
            case SignalType.AdTyping:
                if (message.ContentId is { Length: > 0 } typingThreadId)
                {
                    signals.PublishTyping(new TypingSignal(message.Type, typingThreadId));
                }

                return;
            case SignalType.MusterPing:
                signals.PublishMuster();
                return;
            case SignalType.AnnouncePing:
                signals.PublishAnnouncements();
                return;
            case SignalType.PollPing:
                signals.PublishPolls();
                return;
            case SignalType.FeedbackPing:
                signals.PublishFeedback();
                return;
            case SignalType.ContentRemoved:
                if (message.ContentId is { Length: > 0 } removedContentId)
                {
                    signals.PublishContentRemoved(
                        new ContentRemovalSignal(message.App, message.ContentKind, removedContentId, message.ParentId));
                }

                return;
        }

        var target = message.Type switch
        {
            SignalType.Incoming => IncomingReceived,
            SignalType.Roster => RosterReceived,
            SignalType.Declined => DeclinedReceived,
            SignalType.Unavailable => UnavailableReceived,
            SignalType.Ended => EndedReceived,
            SignalType.Handled => HandledElsewhereReceived,
            _ => null,
        };

        if (target is null)
        {
            if (!message.Type.StartsWith(SignalType.StreamPrefix, StringComparison.Ordinal)
                && !message.Type.StartsWith(SignalType.JamPrefix, StringComparison.Ordinal))
            {
                AepLog.Warning($"[calls] unhandled-signal type={message.Type} call={message.CallId} reason={message.Reason}");
            }

            return;
        }

        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        target.Invoke(id, message);
    }

    public void Dispose()
    {
        signals.BindSender(null);
        connection.ControlReceived -= OnControl;
        connection.ConnectedChanged -= OnConnected;
        relayConnection.ControlReceived -= OnRelayControl;
        relayConnection.ConnectedChanged -= OnRelayConnected;
        relayConnection.Failed -= OnRelayFailed;
        connection.Dispose();
        relayConnection.Dispose();
    }
}
