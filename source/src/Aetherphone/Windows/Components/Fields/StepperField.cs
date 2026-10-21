using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class StepperField
{
    private const string KeySeed = "##stepperField";
    private const float ButtonWidthFraction = 0.3f;
    private const float GlyphFraction = 0.95f;

    public static void Draw(PhoneTheme theme, Rect rect, string valueText, float scale, Action onDecrement,
        Action onIncrement) =>
        Draw(ControlInk.From(theme), rect, valueText, scale, onDecrement, onIncrement);

    public static void Draw(AppSkin ui, Rect rect, string valueText, float scale, Action onDecrement,
        Action onIncrement) =>
        Draw(ui.Ink, rect, valueText, scale, onDecrement, onIncrement);

    private static void Draw(in ControlInk ink, Rect rect, string valueText, float scale, Action onDecrement,
        Action onIncrement)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = MathF.Min(RoundButton.RegularRadius * scale,
            MathF.Min(rect.Height * 0.5f, rect.Width * ButtonWidthFraction * 0.5f));
        var gap = Metrics.Space.Sm * scale;
        var field = new Rect(new Vector2(rect.Min.X + radius * 2f + gap, rect.Min.Y),
            new Vector2(rect.Max.X - radius * 2f - gap, rect.Max.Y));
        SearchBar.Surface(drawList, field, ink);
        if (DrawStep(drawList, new Vector2(rect.Min.X + radius, rect.Center.Y), radius, PhoneIcons.ChevronLeft, ink))
        {
            onDecrement();
        }

        if (DrawStep(drawList, new Vector2(rect.Max.X - radius, rect.Center.Y), radius, PhoneIcons.ChevronRight, ink))
        {
            onIncrement();
        }

        var maxWidth = MathF.Max(1f, field.Width - Metrics.Space.Lg * 2f * scale);
        Typography.DrawCentered(drawList, field.Center, Typography.FitText(valueText, maxWidth, TextStyles.Headline),
            ink.Ink, TextStyles.Headline);
    }

    private static bool DrawStep(ImDrawListPtr drawList, Vector2 center, float radius, string glyph,
        in ControlInk ink)
    {
        var clicked = RoundButton.Draw(drawList, FrameKeys.Next(KeySeed), center, radius, ink, ButtonStyle.Gray, true,
            false, out var face);
        PhoneIcon.Draw(drawList, center, glyph, face.LabelInk,
            radius * GlyphFraction * (face.Face.Width / MathF.Max(radius * 2f, 0.0001f)));
        return clicked;
    }
}
