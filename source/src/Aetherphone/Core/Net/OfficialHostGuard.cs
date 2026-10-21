using System.Net.WebSockets;
using System.Net.Sockets;

namespace Aetherphone.Core.Net;

/// <summary>
/// The single gate for outbound connections. Every HttpClient and ClientWebSocket in the plugin is created
/// here, and the official Aethernet hosts are refused before DNS resolution. This file is also the only
/// .cs file allowed to name those hosts; OfflineGuaranteeTests enforces both rules.
/// </summary>
internal static class OfficialHostGuard
{
    private static readonly string[] BlockedDomains = { "aetherphone.net", "up.railway.app" };

    public static bool IsBlocked(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        var trimmed = host.TrimEnd('.');
        for (var index = 0; index < BlockedDomains.Length; index++)
        {
            var domain = BlockedDomains[index];
            if (trimmed.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void EnsureAllowed(Uri uri)
    {
        if (IsBlocked(uri.Host))
        {
            throw new HttpRequestException($"Connection to official Aethernet host {uri.Host} is disabled in this build.");
        }
    }

    public static bool IsBlockedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile && IsBlocked(uri.Host);

    private static SocketsHttpHandler CreateHandler() => new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        ConnectCallback = ConnectAsync,
    };

    public static HttpClient CreateClient(bool allowAutoRedirect = true, TimeSpan? timeout = null)
    {
        var handler = CreateHandler();
        handler.AllowAutoRedirect = allowAutoRedirect;
        var client = new HttpClient(handler);
        if (timeout is { } value)
        {
            client.Timeout = value;
        }

        return client;
    }

    /// <summary>Builds a client whose pipeline ends in the guarded handler; <paramref name="wrap"/> may only
    /// layer delegating handlers on top of it.</summary>
    public static HttpClient CreateClient(Func<HttpMessageHandler, HttpMessageHandler> wrap) =>
        new(wrap(CreateHandler()));

    public static async Task<ClientWebSocket> ConnectWebSocketAsync(Uri uri, Action<ClientWebSocketOptions>? configure,
        CancellationToken token)
    {
        EnsureAllowed(uri);
        var socket = new ClientWebSocket();
        try
        {
            configure?.Invoke(socket.Options);
            socket.Options.CollectHttpResponseDetails = true;
            await socket.ConnectAsync(uri, token).ConfigureAwait(false);
            return socket;
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            var status = (int)socket.HttpStatusCode;
            socket.Dispose();
            throw new GuardedWebSocketException(status == 0 ? null : status, exception);
        }
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken token)
    {
        if (IsBlocked(context.DnsEndPoint.Host) || IsBlocked(context.InitialRequestMessage.RequestUri?.Host))
        {
            throw new HttpRequestException(
                $"Connection to official Aethernet host {context.InitialRequestMessage.RequestUri?.Host ?? context.DnsEndPoint.Host} is disabled in this build.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, token).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
