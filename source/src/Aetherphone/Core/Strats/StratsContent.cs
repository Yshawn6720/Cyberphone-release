namespace Aetherphone.Core.Strats;

internal static class StratsContent
{
    public const string AppId = "strats";
    // Strats is hidden in Cyberphone: its guides live on the official media CDN, which this build never contacts.
    public const string BaseUrl = "https://strats.cyberphone.invalid/";
    public const string ManifestKey = "guides/wtfdig/manifest.json";
    public const int SchemaVersion = 1;

    public static string Url(string key) => string.Concat(BaseUrl, key);

    public static string ManifestUrl(DateTime utcNow) =>
        string.Concat(BaseUrl, ManifestKey, "?v=", utcNow.ToString("yyyyMMddHH"));
}
