using System.Net;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal enum RelayRefusal : byte
{
    None,
    RoomsFull,
    RateLimited,
    CallsDisabled,
    AddressRooms,
}

internal readonly record struct RelayFailure(int? Status, RelayRefusal Refusal, int RetryAfterSeconds, int MaxRooms)
{
    // The relay's connect limiter counts over a 60 second window (RATE_RETRY_AFTER_SECONDS in server/src/protocol.ts).
    private const int DefaultRetryAfterSeconds = 60;
    private const string RoomsFullReason = "rooms_full";
    private const string RateLimitedReason = "rate_limited";
    private const string CallsDisabledReason = "calls_disabled";
    private const string AddressRoomsReason = "address_rooms";

    public static RelayFailure FromStatus(int? status) => status switch
    {
        (int)HttpStatusCode.ServiceUnavailable => new RelayFailure(status, RelayRefusal.RoomsFull, 0, 0),
        (int)HttpStatusCode.TooManyRequests => new RelayFailure(status, RelayRefusal.RateLimited,
            DefaultRetryAfterSeconds, 0),
        (int)HttpStatusCode.Forbidden => new RelayFailure(status, RelayRefusal.CallsDisabled, 0, 0),
        _ => new RelayFailure(status, RelayRefusal.None, 0, 0),
    };

    public static RelayFailure FromRefusal(CallControl control) => control.Reason switch
    {
        RoomsFullReason => new RelayFailure(null, RelayRefusal.RoomsFull, 0, control.MaxRooms ?? 0),
        RateLimitedReason => new RelayFailure(null, RelayRefusal.RateLimited,
            control.RetryAfterSeconds is int seconds && seconds > 0 ? seconds : DefaultRetryAfterSeconds, 0),
        CallsDisabledReason => new RelayFailure(null, RelayRefusal.CallsDisabled, 0, 0),
        AddressRoomsReason => new RelayFailure(null, RelayRefusal.AddressRooms, 0, control.MaxRooms ?? 0),
        _ => new RelayFailure(null, RelayRefusal.None, 0, 0),
    };
}
