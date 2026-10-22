using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;

namespace Aetherphone.Core.Telephony;

internal sealed class CallRoomRouter : IDisposable
{
    private readonly PartyRelay relay;
    private readonly RealtimeConnection connection;
    private readonly object requestsGate = new();
    private volatile string code = string.Empty;
    private volatile bool awaitingApproval;
    private ParticipantInfo[] joinRequests = Array.Empty<ParticipantInfo>();

    public CallRoomRouter(PartyRelay relay)
    {
        this.relay = relay;
        connection = new RealtimeConnection(relay.Endpoint);
        connection.ControlReceived += OnControl;
        connection.ConnectedChanged += OnConnectedChanged;
        connection.Failed += OnFailed;
    }

    public event Action<CallControl>? Received;
    public event Action<bool>? ConnectedChanged;
    public event Action<RelayFailure>? Failed;

    public PartyRelay Relay => relay;

    public RealtimeConnection Connection => connection;

    public bool Active => relay.Active;

    public bool Connected => relay.Active && connection.Connected;

    public string Code => code;

    public bool AwaitingApproval => awaitingApproval;

    public ReadOnlySpan<ParticipantInfo> JoinRequests => Volatile.Read(ref joinRequests);

    public bool Begin(string? roomCode)
    {
        connection.Stop();
        ResetApproval();
        if (!relay.Begin(roomCode))
        {
            relay.End();
            return false;
        }

        code = roomCode ?? string.Empty;
        connection.Start();
        return true;
    }

    public void End()
    {
        code = string.Empty;
        ResetApproval();
        if (!relay.Active)
        {
            return;
        }

        relay.End();
        connection.Stop();
    }

    public bool Leave()
    {
        if (!Connected)
        {
            return false;
        }

        _ = connection.SendControlAsync(new CallControl { Type = SignalType.Leave });
        return true;
    }

    public bool Approve(string userId) => Decide(SignalType.CallApprove, userId);

    public bool Deny(string userId) => Decide(SignalType.CallDeny, userId);

    private bool Decide(string type, string userId)
    {
        if (!Connected || !RemoveJoinRequest(userId))
        {
            return false;
        }

        _ = connection.SendControlAsync(new CallControl { Type = type, UserId = userId });
        return true;
    }

    public string AdoptInvite(string text)
    {
        if (!text.Contains(PartyRelayLink.Separator))
        {
            return PartyCode.Normalize(text);
        }

        if (!PartyRelayLink.TryParse(text, out var link))
        {
            return string.Empty;
        }

        relay.SaveServer(link.Server, link.Invite);
        return link.Code;
    }

    public string ShareInvite()
    {
        var roomCode = code;
        if (roomCode.Length == 0)
        {
            return string.Empty;
        }

        return relay.TryGetSessionServer(out var server, out var invite)
            ? PartyRelayLink.Format(server, invite, roomCode)
            : roomCode;
    }

    private void OnConnectedChanged(bool connected)
    {
        var roomCode = code;
        if (connected && relay.Active)
        {
            ClearJoinRequests();
            _ = connection.SendControlAsync(new CallControl
            {
                Type = SignalType.Join, Code = roomCode.Length > 0 ? roomCode : null, ApprovalRequired = true,
            });
        }

        ConnectedChanged?.Invoke(connected);
    }

    private void OnControl(CallControl message)
    {
        if (!message.Type.StartsWith(SignalType.CallPrefix, StringComparison.Ordinal))
        {
            return;
        }

        switch (message.Type)
        {
            case SignalType.Roster when message.Code is { Length: > 0 } roomCode && relay.Active:
                code = roomCode;
                awaitingApproval = false;
                relay.SetCode(roomCode);
                relay.SetRole(PartyRelayRole.Viewer);
                break;
            case SignalType.CallJoinPending:
                awaitingApproval = true;
                break;
            case SignalType.CallJoinRequest:
                AddJoinRequest(message.From);
                break;
            case SignalType.CallJoinCancelled:
                RemoveJoinRequest(message.UserId);
                break;
            case SignalType.Declined:
            case SignalType.Ended:
                relay.StopReconnecting();
                break;
        }

        Received?.Invoke(message);
    }

    private void AddJoinRequest(ParticipantInfo? request)
    {
        if (request is null || string.IsNullOrEmpty(request.UserId))
        {
            return;
        }

        lock (requestsGate)
        {
            var current = joinRequests;
            for (var index = 0; index < current.Length; index++)
            {
                if (string.Equals(current[index].UserId, request.UserId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            var grown = new ParticipantInfo[current.Length + 1];
            Array.Copy(current, grown, current.Length);
            grown[^1] = request;
            Volatile.Write(ref joinRequests, grown);
        }
    }

    private bool RemoveJoinRequest(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        lock (requestsGate)
        {
            var current = joinRequests;
            var found = -1;
            for (var index = 0; index < current.Length; index++)
            {
                if (string.Equals(current[index].UserId, userId, StringComparison.Ordinal))
                {
                    found = index;
                    break;
                }
            }

            if (found < 0)
            {
                return false;
            }

            var shrunk = new ParticipantInfo[current.Length - 1];
            Array.Copy(current, 0, shrunk, 0, found);
            Array.Copy(current, found + 1, shrunk, found, current.Length - found - 1);
            Volatile.Write(ref joinRequests, shrunk);
            return true;
        }
    }

    private void ClearJoinRequests()
    {
        lock (requestsGate)
        {
            Volatile.Write(ref joinRequests, Array.Empty<ParticipantInfo>());
        }
    }

    private void ResetApproval()
    {
        awaitingApproval = false;
        ClearJoinRequests();
    }

    private void OnFailed(RelayFailure failure) => Failed?.Invoke(failure);

    public void Dispose()
    {
        connection.ControlReceived -= OnControl;
        connection.ConnectedChanged -= OnConnectedChanged;
        connection.Failed -= OnFailed;
        connection.Dispose();
    }
}
