namespace Aetherphone.Core.Emoji;

internal static class EmojiFavorites
{
    public const int Capacity = 16;

    private static readonly List<string> Unavailable = new();

    public static List<string> Codes
    {
        get
        {
            var configuration = Plugin.Cfg;
            return configuration is null ? Unavailable : configuration.LinkpearlEmojiFavorites;
        }
    }

    public static void Use(string shortcode)
    {
        var configuration = Plugin.Cfg;
        if (configuration is null || !Promote(configuration.LinkpearlEmojiFavorites, shortcode))
        {
            return;
        }

        configuration.Save();
    }

    public static bool Promote(List<string> codes, string shortcode)
    {
        if (shortcode.Length == 0)
        {
            return false;
        }

        var known = codes.IndexOf(shortcode);
        if (known == 0)
        {
            return false;
        }

        if (known > 0)
        {
            codes.RemoveAt(known);
        }

        codes.Insert(0, shortcode);
        while (codes.Count > Capacity)
        {
            codes.RemoveAt(codes.Count - 1);
        }

        return true;
    }
}
