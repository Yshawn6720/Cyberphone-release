using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

// Asks the relay which discoverable rooms stand in the player's zone. It only runs when a nearby list opens or the
// player taps refresh, never on a timer, and keeps 15 seconds between answers because the relay allows each address a
// short burst and then one list every 15 seconds.
internal sealed class RelayNearbyQuery
{
    public const long MinimumGapMilliseconds = 15_000;
    private const string NearbyPath = "/rooms/nearby";
    private const int NotFoundStatus = 404;

    private readonly HttpService http;
    private readonly Configuration configuration;
    private readonly string kindQuery;
    private readonly object gate = new();

    private string? checkedUrl;
    private string? checkedInvite;
    private Uri? endpoint;
    private string bearer = string.Empty;
    private long askedAtMilliseconds;
    private bool asked;
    private volatile bool loading;
    private volatile bool unsupported;
    private CallControl? answer;

    public RelayNearbyQuery(HttpService http, Configuration configuration, PartyRelayKind kind)
    {
        this.http = http;
        this.configuration = configuration;
        kindQuery = kind == PartyRelayKind.Jam ? "?kind=jam&territory=" : "?kind=party&territory=";
    }

    public bool IsConfigured
    {
        get
        {
            lock (gate)
            {
                RefreshConfiguration();
                return endpoint is not null;
            }
        }
    }

    public bool Loading => loading;

    public bool Unsupported => unsupported;

    public bool CanAsk(long nowMilliseconds)
    {
        lock (gate)
        {
            RefreshConfiguration();
            return endpoint is not null && !loading
                && (!asked || nowMilliseconds - askedAtMilliseconds >= MinimumGapMilliseconds);
        }
    }

    public bool TryAsk(uint territoryId, uint worldId, long nowMilliseconds)
    {
        string target;
        string authorization;
        lock (gate)
        {
            RefreshConfiguration();
            if (endpoint is null || territoryId == 0 || worldId == 0 || loading
                || (asked && nowMilliseconds - askedAtMilliseconds < MinimumGapMilliseconds))
            {
                return false;
            }

            asked = true;
            askedAtMilliseconds = nowMilliseconds;
            loading = true;
            target = string.Concat(endpoint.AbsoluteUri, kindQuery,
                territoryId.ToString(CultureInfo.InvariantCulture), "&world=",
                worldId.ToString(CultureInfo.InvariantCulture));
            authorization = bearer;
        }

        _ = Task.Run(() => FetchAsync(target, authorization));
        return true;
    }

    public bool TryTake([NotNullWhen(true)] out CallControl? message)
    {
        message = Interlocked.Exchange(ref answer, null);
        return message is not null;
    }

    private async Task FetchAsync(string target, string authorization)
    {
        try
        {
            var message = await http.GetJsonAsync(target, TelephonyJsonContext.Default.CallControl, authorization,
                CancellationToken.None, status => unsupported = status == NotFoundStatus).ConfigureAwait(false);
            if (message is not null)
            {
                Interlocked.Exchange(ref answer, message);
            }
        }
        finally
        {
            loading = false;
        }
    }

    private void RefreshConfiguration()
    {
        var url = configuration.PartyRelayUrl;
        var invite = configuration.PartyRelayInvite;
        if (ReferenceEquals(url, checkedUrl) && ReferenceEquals(invite, checkedInvite))
        {
            return;
        }

        checkedUrl = url;
        checkedInvite = invite;
        Uri? created = null;
        var usable = !string.IsNullOrWhiteSpace(invite) && RelayAddress.TryCreate(url, NearbyPath, false, out created);
        endpoint = usable ? created : null;
        bearer = usable ? invite.Trim() : string.Empty;
        asked = false;
        unsupported = false;
    }
}
