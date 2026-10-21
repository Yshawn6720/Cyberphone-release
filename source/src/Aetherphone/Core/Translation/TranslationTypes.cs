namespace Aetherphone.Core.Translation;

internal enum TranslationSurface : byte
{
    Post,
    Comment,
    Dm,
    Bio,
    Ad,
    Muster,
    Venue,
    Story,
}

internal enum TranslationState : byte
{
    Idle,
    Loading,
    Shown,
    Hidden,
    SameLanguage,
    Failed,
    Quota,
}

internal readonly record struct TranslationKey(TranslationSurface Surface, string Id);

internal sealed class TranslationEntry
{
    public volatile TranslationState State;
    public string Translated = string.Empty;
    public string SourceLang = string.Empty;
    public long FailedAtTicks;
    public readonly string LayoutKey;
    public TranslationState LabelState;
    public string LabelLanguage = string.Empty;
    public string Label = string.Empty;
    public string ActionLabel = string.Empty;

    public TranslationEntry(string id)
    {
        LayoutKey = id + "|t";
    }

    public bool Showing => State == TranslationState.Shown;
}

internal readonly struct TranslationView
{
    public readonly string Text;
    public readonly string LayoutKey;
    public readonly TranslationEntry Entry;

    public TranslationView(string text, string layoutKey, TranslationEntry entry)
    {
        Text = text;
        LayoutKey = layoutKey;
        Entry = entry;
    }

    public bool Showing => Entry.Showing;
}
