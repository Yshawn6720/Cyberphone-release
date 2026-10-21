using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Telephony;

internal static class CallStatusText
{
    private static int cachedSeconds = -1;
    private static string cachedDuration = string.Empty;

    public static string Label(in CallView view)
    {
        if (!view.Connected)
        {
            return Loc.T(L.Phone.Reconnecting);
        }

        return view.State switch
        {
            CallState.Dialing => Loc.T(L.Phone.StatusCalling),
            CallState.Connecting => Loc.T(L.Phone.StatusConnecting),
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
