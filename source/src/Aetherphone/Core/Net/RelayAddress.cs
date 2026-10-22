using System.Diagnostics.CodeAnalysis;

namespace Aetherphone.Core.Net;

internal static class RelayAddress
{
    public static bool TryCreate(string? server, string path, bool webSocket, [NotNullWhen(true)] out Uri? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(server) || !Uri.TryCreate(server.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        string scheme;
        if (parsed.Scheme == Uri.UriSchemeHttps)
        {
            scheme = webSocket ? Uri.UriSchemeWss : Uri.UriSchemeHttps;
        }
        else if (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback)
        {
            scheme = webSocket ? Uri.UriSchemeWs : Uri.UriSchemeHttp;
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
        endpoint = new Uri(string.Concat(scheme, withoutQuery.AsSpan(parsed.Scheme.Length), path));
        return true;
    }

    public static string Host(string? server)
    {
        return !string.IsNullOrWhiteSpace(server) && Uri.TryCreate(server.Trim(), UriKind.Absolute, out var parsed)
            ? parsed.Host
            : string.Empty;
    }
}
