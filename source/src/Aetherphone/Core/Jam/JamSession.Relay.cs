using System.Net;
using System.Runtime.CompilerServices;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Video;

namespace Aetherphone.Core.Jam;

internal sealed partial class JamSession
{
    private StrongBox<RelayFailure>? pendingRelayFailure;
    private bool handshakePending;

    public bool RelayMode => !HiddenApps.AethernetEnabled;

    public bool RelayReady => signals.Relay.RelayReady;

    public string SelfId => MyUserId;

    public RelayFailure LastRelayRefusal { get; private set; }

    public string AdoptInvite(string text)
    {
        if (!RelayMode || !text.Contains(PartyRelayLink.Separator))
        {
            return PartyCode.Normalize(text);
        }

        if (!PartyRelayLink.TryParse(text, out var link))
        {
            return string.Empty;
        }

        signals.Relay.SaveServer(link.Server, link.Invite);
        return link.Code;
    }

    public string ShareInvite()
    {
        if (!RelayMode || Code.Length == 0)
        {
            return DisplayCode;
        }

        return signals.Relay.TryGetSessionServer(out var server, out var invite)
            ? PartyRelayLink.Format(server, invite, Code)
            : DisplayCode;
    }

    private void OnRelayFailed(RelayFailure failure) =>
        Interlocked.Exchange(ref pendingRelayFailure, new StrongBox<RelayFailure>(failure));

    private bool OpenRelay(string? code, bool nearby)
    {
        if (!RelayMode)
        {
            return true;
        }

        if (!signals.BeginRelay(code, nearby))
        {
            Decline(JamDeclineReason.ServerMissing);
            return false;
        }

        handshakePending = true;
        return true;
    }

    private void SendHandshake(long now)
    {
        if (!handshakePending || !signals.Connected)
        {
            return;
        }

        handshakePending = false;
        awaitingSinceTicks = now;
        if (Mode == JamMode.Starting)
        {
            signals.Start(Title.Length > 0 ? Title : null);
            return;
        }

        signals.Join(Code);
    }

    private void ApplyRelayFailure()
    {
        if (Interlocked.Exchange(ref pendingRelayFailure, null) is not { } pending || Mode == JamMode.Idle)
        {
            return;
        }

        var failure = pending.Value;
        if (failure.Refusal != RelayRefusal.None)
        {
            LastRelayRefusal = failure;
            ExitLocal(JamDeclineReason.RelayRefused);
            return;
        }

        ExitLocal(failure.Status switch
        {
            (int)HttpStatusCode.Unauthorized => JamDeclineReason.InviteRejected,
            (int)HttpStatusCode.NotFound => InJam ? JamDeclineReason.Ended : JamDeclineReason.BadCode,
            (int)HttpStatusCode.Conflict => JamDeclineReason.Full,
            _ => JamDeclineReason.ConnectionLost,
        });
    }

    private void BindRelay(PartyRelayRole role)
    {
        if (!RelayMode || Code.Length == 0)
        {
            return;
        }

        signals.Relay.SetCode(Code);
        signals.Relay.SetRole(role);
    }
}
