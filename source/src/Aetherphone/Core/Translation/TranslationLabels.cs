using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Translation;

internal static class TranslationLabels
{
    public static void Resolve(TranslationEntry entry, out string label, out string action)
    {
        var state = entry.State;
        var language = Loc.Current.Code;
        if (entry.LabelState != state || !string.Equals(entry.LabelLanguage, language, StringComparison.Ordinal))
        {
            Build(entry, state, language);
        }

        label = entry.Label;
        action = entry.ActionLabel;
    }

    public static string SourceLanguageName(string code)
    {
        var languages = Languages.All;
        for (var index = 0; index < languages.Length; index++)
        {
            if (string.Equals(languages[index].Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return languages[index].NativeName;
            }
        }

        return string.Empty;
    }

    private static void Build(TranslationEntry entry, TranslationState state, string language)
    {
        var label = string.Empty;
        var action = string.Empty;
        switch (state)
        {
            case TranslationState.Loading:
                label = Loc.T(L.Translate.Pending);
                break;
            case TranslationState.Shown:
                var sourceName = SourceLanguageName(entry.SourceLang);
                label = sourceName.Length > 0
                    ? Loc.T(L.Translate.TranslatedFrom, sourceName)
                    : Loc.T(L.Translate.Translated);
                action = Loc.T(L.Translate.ShowOriginal);
                break;
            case TranslationState.Hidden:
                action = Loc.T(L.Translate.ShowTranslation);
                break;
            case TranslationState.SameLanguage:
                label = Loc.T(L.Translate.SameLanguage);
                break;
            case TranslationState.Failed:
                action = Loc.T(L.Translate.Failed);
                break;
            case TranslationState.Quota:
                label = Loc.T(L.Translate.Quota);
                break;
        }

        entry.Label = label;
        entry.ActionLabel = action;
        entry.LabelState = state;
        entry.LabelLanguage = language;
    }
}
