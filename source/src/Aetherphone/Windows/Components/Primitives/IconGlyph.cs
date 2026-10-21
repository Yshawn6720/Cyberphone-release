using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class IconGlyph
{
    private static readonly Dictionary<FontAwesomeIcon, string> Cache = new();

    public static string Of(FontAwesomeIcon icon)
    {
        if (Cache.TryGetValue(icon, out var cached))
        {
            return cached;
        }

        var glyph = icon.ToIconString();
        Cache[icon] = glyph;
        return glyph;
    }
}
