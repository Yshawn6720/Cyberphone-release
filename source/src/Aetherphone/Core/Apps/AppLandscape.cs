namespace Aetherphone.Core.Apps;

internal static class AppLandscape
{
    private static string holder = string.Empty;

    public static bool Held(string appId) => holder.Length > 0 && string.Equals(holder, appId, StringComparison.Ordinal);

    public static void Request(string appId) => holder = appId;

    public static void Release(string appId)
    {
        if (Held(appId))
        {
            holder = string.Empty;
        }
    }
}
