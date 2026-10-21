using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class SensitiveVeil
{
    private const float CompactEdge = 132f;

    private static readonly Vector4 Ground = new(0.03f, 0.03f, 0.04f, 1f);
    private static readonly Vector4 StrongInk = new(1f, 1f, 1f, 0.94f);
    private static readonly Vector4 SoftInk = new(1f, 1f, 1f, 0.72f);

    public static void Draw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding)
    {
        var scale = UiScale.Current;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Ground));

        var width = max.X - min.X;
        var height = max.Y - min.Y;
        var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
        var compact = width < CompactEdge * scale || height < CompactEdge * scale;
        if (compact)
        {
            AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.EyeSlash), StrongInk, 1.15f);
            HintHand(min, max);
            return;
        }

        var textWidth = MathF.Max(1f, width - 24f * scale);
        var title = Typography.FitText(Loc.T(L.Moderation.SensitiveTitle), textWidth, TextStyles.Headline);
        var hint = Typography.FitText(Loc.T(L.Moderation.SensitiveReveal), textWidth, TextStyles.Footnote);

        AppSkin.Icon(drawList, new Vector2(center.X, center.Y - 28f * scale), IconGlyph.Of(FontAwesomeIcon.EyeSlash),
            StrongInk, 1.6f);
        Typography.DrawCentered(drawList, center, title, StrongInk, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + 22f * scale), hint, SoftInk,
            TextStyles.Footnote);
        HintHand(min, max);
    }

    private static void HintHand(Vector2 min, Vector2 max)
    {
        if (UiInteract.Hover(min, max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
}
