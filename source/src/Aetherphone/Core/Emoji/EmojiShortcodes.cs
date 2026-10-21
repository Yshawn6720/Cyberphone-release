namespace Aetherphone.Core.Emoji;

internal static class EmojiShortcodes
{
    public static bool Enabled
    {
        get
        {
            var configuration = Plugin.Cfg;
            return configuration is null || configuration.LinkpearlEmojiShortcodes;
        }
    }

    public static bool MightContain(ReadOnlySpan<char> text) => Enabled && EmojiScanner.MightContain(text);

    public static void Collect(string text, List<EmojiSpan> target)
    {
        if (!Enabled)
        {
            return;
        }

        EmojiScanner.Collect(text, target);
    }

    public static bool TryResolve(ReadOnlySpan<char> shortcode, out string file) =>
        EmojiCatalog.TryResolve(shortcode, out file);
}
