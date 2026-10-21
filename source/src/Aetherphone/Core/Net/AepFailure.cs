using System.Text.Json.Serialization;

namespace Aetherphone.Core.Net;

internal enum AepFailureKind : byte
{
    None = 0,
    Offline = 1,
    Timeout = 2,
    RateLimitPaused = 3,
    SignedOut = 4,
    Server = 5,
    BadResponse = 6,
    Cancelled = 7,
}

internal readonly record struct AepFailure(
    AepFailureKind Kind,
    int StatusCode,
    string? Code,
    string? ServerMessage,
    string? RequestId,
    string? Value)
{
    public static readonly AepFailure None = default;

    public bool Failed => Kind != AepFailureKind.None;

    public static AepFailure Transport(AepFailureKind kind) => new(kind, 0, null, null, null, null);

    public static AepFailure FromStatus(int statusCode, string? requestId) =>
        new(AepFailureKind.Server, statusCode, null, null, requestId, null);

    public string Reference()
    {
        if (!string.IsNullOrEmpty(RequestId))
        {
            return RequestId;
        }

        return StatusCode > 0 ? StatusCode.ToString() : Kind.ToString();
    }

    public string Describe()
    {
        var code = string.IsNullOrEmpty(Code) ? "-" : Code;
        var reference = string.IsNullOrEmpty(RequestId) ? "-" : RequestId;
        return $"{Kind} status={StatusCode} code={code} req={reference}";
    }
}

internal sealed class AepFailureBox
{
    public AepFailureBox(AepFailure failure)
    {
        Failure = failure;
    }

    public AepFailure Failure { get; }
}

internal sealed record AepErrorBody(string? Error, string? Code, string? RequestId, string? Value);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AepErrorBody))]
internal sealed partial class AepErrorJsonContext : JsonSerializerContext;
