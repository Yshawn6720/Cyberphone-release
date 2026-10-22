using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Telephony;

internal static class CallStatusText
{
    private static int cachedSeconds = -1;
    private static string cachedDuration = string.Empty;

    public static string Label(in CallView view)
    {
        var relay = CallHub.RelayMode;
        if (!view.Connected && !(relay && view.State == CallState.Connecting))
        {
            return Loc.T(L.Phone.Reconnecting);
        }

        return view.State switch
        {
            CallState.Dialing => Loc.T(relay ? L.Phone.RoomWaiting : L.Phone.StatusCalling),
            CallState.Connecting => Loc.T(view.AwaitingApproval ? L.Phone.RoomAwaitingHost : L.Phone.StatusConnecting),
            CallState.Active => Duration(view.Seconds),
            _ => string.Empty,
        };
    }

    private static string Duration(int seconds)
    {
        if (seconds != cachedSeconds)
        {
            cachedSeconds = seconds;
            cachedDuration = TimeText.Duration(seconds);
        }

        return cachedDuration;
    }
}
