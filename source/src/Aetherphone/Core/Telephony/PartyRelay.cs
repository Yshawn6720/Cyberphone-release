using System.Net.WebSockets;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Telephony;

internal readonly record struct PartyRelayIdentity(string Name, string World);

internal enum PartyRelayRole : byte
{
    Host,
    Viewer,
}

internal enum PartyRelayKind : byte
{
    Party,
    Jam,
    Game,
    Call,
}

internal sealed class PartyRelay
{
    private const string RealtimePath = "/rt";
    private const int MaxIdentityLength = 32;
    private const int ViewerReconnects = 3;
    private const long IdentityRefreshMilliseconds = 1000;

    private readonly Configuration configuration;
    private readonly Func<PartyRelayIdentity?> identitySource;
    private readonly string kindQuery;
    private readonly object gate = new();

    private string? checkedUrl;
    private string? checkedInvite;
    private Uri? configuredUri;
    private bool configured;
    private PartyRelayIdentity? cachedIdentity;
    private string cachedIdentityId = string.Empty;
    private long identityReadAtTicks;
    private bool identityRead;

    private Uri? sessionUri;
    private string sessionServer = string.Empty;
    private string sessionInvite = string.Empty;
    private PartyRelayIdentity sessionIdentity;
    private string? sessionCode;
    private PartyRelayRole sessionRole;
    private bool sessionRemoved;
    private volatile bool active;
    private volatile string? selfId;

    public PartyRelay(Configuration configuration, Func<PartyRelayIdentity?> identitySource,
        PartyRelayKind kind = PartyRelayKind.Party, Action<RelayFailure>? refused = null)
    {
        this.configuration = configuration;
        this.identitySource = identitySource;
        kindQuery = kind switch
        {
            PartyRelayKind.Jam => "?kind=jam&",
            PartyRelayKind.Game => "?kind=game&",
            PartyRelayKind.Call => "?kind=call&",
            _ => "?",
        };
        Endpoint = new RealtimeEndpoint(ResolveUri, Configure, MaxReconnects, refused);
    }

    public RealtimeEndpoint Endpoint { get; }

    public bool Active => active;

    public string? SelfId => selfId;

    public bool IsConfigured
    {
        get
        {
            lock (gate)
            {
                RefreshConfiguration();
                return configured;
            }
        }
    }

    public bool UrlRejected
    {
        get
        {
            lock (gate)
            {
                RefreshConfiguration();
                return configuredUri is null && !string.IsNullOrWhiteSpace(checkedUrl);
            }
        }
    }

    public bool RelayReady
    {
        get
        {
            lock (gate)
            {
                RefreshConfiguration();
                return configured && RefreshIdentity() is not null;
            }
        }
    }

    public string LocalId
    {
        get
        {
            lock (gate)
            {
                RefreshIdentity();
                return cachedIdentityId;
            }
        }
    }

    private PartyRelayIdentity? RefreshIdentity()
    {
        var now = Environment.TickCount64;
        if (identityRead && now - identityReadAtTicks < IdentityRefreshMilliseconds)
        {
            return cachedIdentity;
        }

        identityRead = true;
        identityReadAtTicks = now;
        var identity = Validate(identitySource());
        if (!Nullable.Equals(identity, cachedIdentity))
        {
            cachedIdentity = identity;
            cachedIdentityId = identity is { } known ? string.Concat(known.Name, "@", known.World) : string.Empty;
        }

        return cachedIdentity;
    }

    internal static bool IsUsableServer(string url) => RelayAddress.TryCreate(url, RealtimePath, true, out _);

    public bool Begin(string? code)
    {
        lock (gate)
        {
            RefreshConfiguration();
            if (!configured || configuredUri is null)
            {
                return false;
            }

            if (Validate(identitySource()) is not { } identity)
            {
                return false;
            }

            sessionUri = configuredUri;
            sessionServer = configuration.PartyRelayUrl.Trim();
            sessionInvite = configuration.PartyRelayInvite.Trim();
            sessionIdentity = identity;
            sessionCode = code;
            sessionRole = code is null ? PartyRelayRole.Host : PartyRelayRole.Viewer;
            sessionRemoved = false;
            selfId = string.Concat(identity.Name, "@", identity.World);
            active = true;
            return true;
        }
    }

    public void End()
    {
        lock (gate)
        {
            active = false;
            selfId = null;
            sessionUri = null;
            sessionServer = string.Empty;
            sessionInvite = string.Empty;
            sessionCode = null;
            sessionRole = PartyRelayRole.Host;
            sessionRemoved = false;
        }
    }

    public bool TryGetSessionServer(out string server, out string invite)
    {
        lock (gate)
        {
            server = sessionServer;
            invite = sessionInvite;
            return active && server.Length > 0;
        }
    }

    public void SetRole(PartyRelayRole role)
    {
        lock (gate)
        {
            sessionRole = role;
        }
    }

    public void StopReconnecting()
    {
        lock (gate)
        {
            sessionRemoved = true;
        }
    }

    public void SetCode(string code)
    {
        lock (gate)
        {
            sessionCode = code;
        }
    }

    public void SaveServer(string url, string invite)
    {
        if (string.Equals(url, configuration.PartyRelayUrl, StringComparison.Ordinal)
            && string.Equals(invite, configuration.PartyRelayInvite, StringComparison.Ordinal))
        {
            return;
        }

        configuration.PartyRelayUrl = url;
        configuration.PartyRelayInvite = invite;
        configuration.Save();
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
        configuredUri = RelayAddress.TryCreate(url, RealtimePath, true, out var realtimeUri) ? realtimeUri : null;
        configured = configuredUri is not null && !string.IsNullOrWhiteSpace(invite);
    }

    private static PartyRelayIdentity? Validate(PartyRelayIdentity? candidate)
    {
        if (candidate is not { } identity)
        {
            return null;
        }

        var name = identity.Name;
        var world = identity.World;
        if (string.IsNullOrEmpty(name) || name.Length > MaxIdentityLength || name.Contains('@'))
        {
            return null;
        }

        if (string.IsNullOrEmpty(world) || world.Length > MaxIdentityLength)
        {
            return null;
        }

        return identity;
    }

    private Uri ResolveUri()
    {
        lock (gate)
        {
            if (sessionUri is null)
            {
                throw new InvalidOperationException("The party relay was not begun.");
            }

            var name = Uri.EscapeDataString(sessionIdentity.Name);
            var world = Uri.EscapeDataString(sessionIdentity.World);
            var query = sessionCode is { } code
                ? string.Concat(kindQuery, "code=", Uri.EscapeDataString(code), "&name=", name, "&world=", world)
                : string.Concat(kindQuery, "create=1&name=", name, "&world=", world);
            return new Uri(sessionUri.AbsoluteUri + query);
        }
    }

    private void Configure(ClientWebSocketOptions options)
    {
        string invite;
        lock (gate)
        {
            invite = sessionInvite;
        }

        options.SetRequestHeader("Authorization", "Bearer " + invite);
    }

    private int? MaxReconnects()
    {
        lock (gate)
        {
            return !sessionRemoved && sessionRole == PartyRelayRole.Viewer && sessionCode is not null
                ? ViewerReconnects
                : 0;
        }
    }
}
