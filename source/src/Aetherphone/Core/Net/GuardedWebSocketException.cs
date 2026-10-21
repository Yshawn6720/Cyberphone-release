namespace Aetherphone.Core.Net;

internal sealed class GuardedWebSocketException : Exception
{
    public GuardedWebSocketException(int? statusCode, Exception innerException)
        : base(statusCode is { } status
            ? $"WebSocket upgrade was refused with HTTP {status}."
            : "WebSocket connection failed.", innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
