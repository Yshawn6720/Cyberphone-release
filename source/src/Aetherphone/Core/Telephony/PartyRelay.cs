using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Telephony;

internal readonly record struct PartyRelayIdentity(string Name, string World);

internal enum PartyRelayRole : byte
{
    Host,
    Viewer,
}

internal sealed class PartyRelay
{
    private const int MaxIdentityLength = 32;
    private const int ViewerReconnects = 3;
    private const long IdentityRefreshMilliseconds = 1000;

    private readonly Configuration configuration;
    private readonly Func<PartyRelayIdentity?> identitySource;
    private readonly object gate = new();

    private string? checkedUrl;
    private string? checkedInvite;
    private Uri? configuredUri;
    private bool configured;
    private PartyRelayIdentity? cachedIdentity;
    private long identityReadAtTicks;
    private bool identityRead;

    private Uri? sessionUri;
    private string sessionServer = string.Empty;
    private string sessionInvite = string.Empty;
    private PartyRelayIdentity sessionIdentity;
    private string? sessionCode;
    private PartyRelayRole sessionRole;
    private volatile bool active;
    private volatile string? selfId;

    public PartyRelay(Configuration configuration, Func<PartyRelayIdentity?> identitySource)
    {
        this.configuration = configuration;
        this.identitySource = identitySource;
        Endpoint = new RealtimeEndpoint(ResolveUri, Configure, MaxReconnects);
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
            var now = Environment.TickCount64;
            lock (gate)
            {
                RefreshConfiguration();
                if (!configured)
                {
                    return false;
                }

                if (!identityRead || now - identityReadAtTicks >= IdentityRefreshMilliseconds)
                {
                    identityRead = true;
                    identityReadAtTicks = now;
                    cachedIdentity = Validate(identitySource());
                }

                return cachedIdentity is not null;
            }
        }
    }

    internal static bool IsUsableServer(string url) => TryCreateRealtimeUri(url, out _);

    private static bool TryCreateRealtimeUri(string? url, [NotNullWhen(true)] out Uri? realtimeUri)
    {
        realtimeUri = null;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        string socketScheme;
        if (parsed.Scheme == Uri.UriSchemeHttps)
        {
            socketScheme = "wss";
        }
        else if (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback)
        {
            socketScheme = "ws";
        }
        else
        {
            return false;
        }

        if (OfficialHostGuard.IsBlocked(parsed.Host))
        {
            return false;
        }

        var withoutQuery = parsed.GetLeftPart(UriPartial.Path).TrimEnd('/');
        realtimeUri = new Uri(string.Concat(socketScheme, withoutQuery.AsSpan(parsed.Scheme.Length), "/rt"));
        return true;
    }

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

    public void SetCode(string code)
    {
        lock (gate)
        {
            sessionCode = code;
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
        configuredUri = TryCreateRealtimeUri(url, out var realtimeUri) ? realtimeUri : null;
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
                ? string.Concat("?code=", Uri.EscapeDataString(code), "&name=", name, "&world=", world)
                : string.Concat("?create=1&name=", name, "&world=", world);
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
            return sessionRole == PartyRelayRole.Viewer && sessionCode is not null ? ViewerReconnects : 0;
        }
    }
}
