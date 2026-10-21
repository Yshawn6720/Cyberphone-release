using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class TranslateLink
{
    private const float FontScale = 0.8f;
    private const float Gap = 4f;
    private const float Separator = 6f;

    public static float Height(TranslationService translation, in TranslationKey key, string? lang, float scale)
    {
        var entry = translation.Peek(key);
        if (!Visible(translation, entry, lang))
        {
            return 0f;
        }

        return Typography.LineHeight(new TextStyle(FontScale, FontWeight.Regular)) + Gap * scale;
    }

    public static float Draw(TranslationService translation, ConfirmService confirm, in TranslationKey key,
        string? lang, string text, Vector2 topLeft, float maxWidth, Vector4 mutedInk, Vector4 accent, float scale)
    {
        var entry = translation.Peek(key);
        if (!Visible(translation, entry, lang))
        {
            return 0f;
        }

        string label;
        string action;
        if (entry.State == TranslationState.Idle)
        {
            label = string.Empty;
            action = Loc.T(L.Translate.Action);
        }
        else
        {
            TranslationLabels.Resolve(entry, out label, out action);
        }

        var style = new TextStyle(FontScale, FontWeight.Regular);
        var actionStyle = new TextStyle(FontScale, FontWeight.Medium);
        var lineHeight = Typography.LineHeight(style);
        var drawList = ImGui.GetWindowDrawList();
        var cursorX = topLeft.X;
        if (label.Length > 0)
        {
            var fitted = Typography.FitText(label, maxWidth, style);
            Typography.Draw(drawList, new Vector2(cursorX, topLeft.Y), fitted, mutedInk, style);
            cursorX += Typography.Measure(fitted, style).X + Separator * scale;
        }

        if (action.Length > 0)
        {
            var remaining = MathF.Max(1f, topLeft.X + maxWidth - cursorX);
            var fitted = Typography.FitText(action, remaining, actionStyle);
            var size = Typography.Measure(fitted, actionStyle);
            var actionMin = new Vector2(cursorX, topLeft.Y);
            var actionMax = new Vector2(cursorX + size.X, topLeft.Y + lineHeight);
            var hovered = UiInteract.Hover(actionMin, actionMax);
            Typography.Draw(drawList, actionMin, fitted, hovered ? Palette.Lighten(accent, 0.15f) : accent, actionStyle);
            if (UiInteract.Click(actionMin, actionMax, hovered))
            {
                Activate(translation, confirm, key, text, entry);
            }
        }

        return lineHeight + Gap * scale;
    }

    public static void Activate(TranslationService translation, ConfirmService confirm, in TranslationKey key,
        string text, TranslationEntry entry)
    {
        switch (entry.State)
        {
            case TranslationState.Shown:
            case TranslationState.Hidden:
                translation.ToggleOriginal(key);
                return;
            case TranslationState.Loading:
            case TranslationState.SameLanguage:
            case TranslationState.Quota:
                return;
        }

        RequestWithDisclosure(translation, confirm, key, text);
    }

    public static void RequestWithDisclosure(TranslationService translation, ConfirmService confirm,
        in TranslationKey key, string text)
    {
        var requestKey = key;
        WithDisclosure(translation, confirm, () => translation.Request(requestKey, text));
    }

    public static void WithDisclosure(TranslationService translation, ConfirmService confirm, Action accepted)
    {
        if (translation.DisclosureSeen)
        {
            accepted();
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Translate.DisclosureTitle),
            Message = Loc.T(L.Translate.DisclosureBody),
            ConfirmLabel = Loc.T(L.Translate.DisclosureContinue),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = () =>
            {
                translation.MarkDisclosureSeen();
                accepted();
            },
        });
    }

    private static bool Visible(TranslationService translation, TranslationEntry entry, string? lang)
    {
        if (entry.State != TranslationState.Idle)
        {
            return true;
        }

        return translation.ShouldOffer(lang);
    }
}
