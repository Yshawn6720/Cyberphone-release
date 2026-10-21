using System.Net.WebSockets;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Telephony;

internal sealed class RealtimeEndpoint
{
    public RealtimeEndpoint(Func<Uri> resolveUri, Action<ClientWebSocketOptions> configure, Func<int?> maxReconnects)
    {
        ResolveUri = resolveUri;
        Configure = configure;
        MaxReconnects = maxReconnects;
    }

    public Func<Uri> ResolveUri { get; }
    public Action<ClientWebSocketOptions> Configure { get; }
    public Func<int?> MaxReconnects { get; }

    public static RealtimeEndpoint Aethernet(AethernetSession session) => new(
        () => BuildUri(session.BaseUrl),
        options =>
        {
            var bearer = session.Token;
            if (!string.IsNullOrEmpty(bearer))
            {
                options.SetRequestHeader("Authorization", $"Bearer {bearer}");
            }

            AethernetClientIdentity.Apply(options);
        },
        static () => null);

    private static Uri BuildUri(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = string.Concat("wss://", trimmed.AsSpan("https://".Length));
        }
        else if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = string.Concat("ws://", trimmed.AsSpan("http://".Length));
        }

        return new Uri(trimmed + "/rt");
    }
}
