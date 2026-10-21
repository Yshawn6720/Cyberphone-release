namespace Aetherphone.Core.GameChat;

internal static class ChatDrafts
{
    public const int MaxRecentSent = 30;
    private const long SaveDebounceMilliseconds = 2000;

    private static long dirtyAtMilliseconds;
    private static bool dirty;

    public static string Load(string conversationKey)
    {
        var configuration = Plugin.Cfg;
        if (configuration is null || !configuration.LinkpearlDraftAutosave || conversationKey.Length == 0)
        {
            return string.Empty;
        }

        return configuration.LinkpearlDrafts.TryGetValue(conversationKey, out var stored) ? stored : string.Empty;
    }

    public static void Store(string conversationKey, string text)
    {
        var configuration = Plugin.Cfg;
        if (configuration is null || !configuration.LinkpearlDraftAutosave || conversationKey.Length == 0)
        {
            return;
        }

        var drafts = configuration.LinkpearlDrafts;
        if (IsBlank(text))
        {
            if (drafts.Remove(conversationKey))
            {
                MarkDirty();
            }

            return;
        }

        if (drafts.TryGetValue(conversationKey, out var stored) && string.Equals(stored, text, StringComparison.Ordinal))
        {
            return;
        }

        drafts[conversationKey] = text;
        MarkDirty();
    }

    public static void Forget()
    {
        var configuration = Plugin.Cfg;
        if (configuration is null || configuration.LinkpearlDrafts.Count == 0)
        {
            return;
        }

        configuration.LinkpearlDrafts.Clear();
        MarkDirty();
    }

    public static void Record(string channelKey, string target, string text)
    {
        var configuration = Plugin.Cfg;
        if (configuration is null || text.Length == 0)
        {
            return;
        }

        var recent = configuration.LinkpearlRecentSent;
        recent.Insert(0, new SentMessage
        {
            ChannelKey = channelKey,
            Target = target,
            Text = text,
            SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        while (recent.Count > MaxRecentSent)
        {
            recent.RemoveAt(recent.Count - 1);
        }

        MarkDirty();
    }

    public static void ClearRecent()
    {
        var configuration = Plugin.Cfg;
        if (configuration is null || configuration.LinkpearlRecentSent.Count == 0)
        {
            return;
        }

        configuration.LinkpearlRecentSent.Clear();
        MarkDirty();
        Flush();
    }

    public static void Tick()
    {
        if (!dirty || Environment.TickCount64 - dirtyAtMilliseconds < SaveDebounceMilliseconds)
        {
            return;
        }

        Flush();
    }

    public static void Flush()
    {
        if (!dirty || Plugin.Cfg is not { } configuration)
        {
            return;
        }

        dirty = false;
        configuration.Save();
    }

    private static bool IsBlank(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static void MarkDirty()
    {
        if (dirty)
        {
            return;
        }

        dirty = true;
        dirtyAtMilliseconds = Environment.TickCount64;
    }
}
