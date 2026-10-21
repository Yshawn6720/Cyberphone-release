using System.Globalization;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ListSection
{
    private const int OverlineCacheLimit = 256;
    private static readonly Dictionary<string, string> OverlineCache = new(StringComparer.Ordinal);
    private static CultureInfo? overlineCulture;

    public static float OverlineHeight => Typography.LineHeight(TextStyles.FootnoteEmphasized);

    public static void Label(AppSkin ui, string label) => Header(label, ui.MutedInk);

    public static void Header(string title, Vector4 ink) => Header(title, ink, null, null);

    public static void Header(string title, Vector4 ink, PhoneTheme? theme, string? hint)
    {
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var origin = ImGui.GetCursorScreenPos();
        var left = origin.X + Metrics.Space.Lg * scale;
        var hasHint = theme is not null && hint is not null;
        var hintReserve = hasHint ? (Metrics.Size.HintIconHeight + Metrics.Space.Sm) * scale : 0f;
        var maxWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X - Metrics.Space.Lg * 2f * scale - hintReserve);
        var width = PaintOverline(ImGui.GetWindowDrawList(), new Vector2(left, origin.Y), title, ink, maxWidth);
        var height = OverlineHeight;
        if (hasHint)
        {
            var iconCenter = new Vector2(left + width + Metrics.Size.HintIconHeight * 0.5f * scale
                + Metrics.Space.Sm * scale, origin.Y + height * 0.5f);
            HintIcon.Draw(iconCenter, hint!, theme!, scale);
        }

        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, height));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    public static float PaintOverline(ImDrawListPtr drawList, Vector2 origin, string label, Vector4 mutedInk,
        float maxWidth)
    {
        var text = Typography.FitText(Overline(label), maxWidth, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, origin, text, mutedInk, TextStyles.FootnoteEmphasized);
        return Typography.Measure(text, TextStyles.FootnoteEmphasized).X;
    }

    public static string Overline(string label)
    {
        var culture = Loc.Culture;
        if (!ReferenceEquals(culture, overlineCulture) || OverlineCache.Count >= OverlineCacheLimit)
        {
            overlineCulture = culture;
            OverlineCache.Clear();
        }

        if (OverlineCache.TryGetValue(label, out var cached))
        {
            return cached;
        }

        var upper = culture.TextInfo.ToUpper(label);
        OverlineCache[label] = upper;
        return upper;
    }
}
