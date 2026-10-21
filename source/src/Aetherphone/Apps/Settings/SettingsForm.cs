using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings;

internal static class SettingsForm
{
    private const float CodeCardHeight = 64f;
    private const float StepBadgeDiameter = 22f;
    private const float StepRowPadding = 12f;
    private const float CopiedHoverMix = 0.10f;

    public static void Gap(float units) => ImGui.Dummy(new Vector2(0f, units * UiScale.Current));

    public static void Title(string text, PhoneTheme theme) => Text(text, theme.TextStrong, TextStyles.Title2);

    public static void Heading(string text, PhoneTheme theme) => Text(text, theme.TextStrong, TextStyles.Title3);

    public static void Body(string text, PhoneTheme theme) => Text(text, theme.TextMuted, TextStyles.Subheadline);

    public static void Note(string text, PhoneTheme theme) => Text(text, theme.TextMuted, TextStyles.Footnote);

    public static void Text(string text, Vector4 color, in TextStyle style)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Typography.DrawWrappedLeft(origin, text, color, style, MathF.Max(1f, width));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public static bool Button(string label, PhoneTheme theme, ButtonStyle style = ButtonStyle.Gray,
        ButtonRole role = ButtonRole.Normal, bool enabled = true)
    {
        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Windows.Components.Button.LargeHeight * UiScale.Current);
        ImGui.Dummy(size);
        return Windows.Components.Button.Draw(new Rect(origin, origin + size), label, ControlInk.From(theme), style, role,
            enabled);
    }

    public static void ButtonPair(string leading, string trailing, PhoneTheme theme, out bool leadingClicked,
        out bool trailingClicked)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var spacing = Metrics.Space.Sm * scale;
        var half = (width - spacing) * 0.5f;
        var height = Windows.Components.Button.LargeHeight * scale;
        var ink = ControlInk.From(theme);
        leadingClicked = Windows.Components.Button.Draw(new Rect(origin, origin + new Vector2(half, height)), leading,
            ink, ButtonStyle.Gray);
        var trailingMin = new Vector2(origin.X + half + spacing, origin.Y);
        trailingClicked = Windows.Components.Button.Draw(new Rect(trailingMin, trailingMin + new Vector2(half, height)),
            trailing, ink, ButtonStyle.Gray);
        ImGui.Dummy(new Vector2(width, height));
    }

    public static bool ActionCard(string label, Vector4 color, PhoneTheme theme, bool enabled = true)
    {
        var card = GroupCard.Begin(theme, 1);
        var clicked = SettingsRow.Action(card.NextRow(), label, enabled ? color : theme.TextMuted, theme, enabled);
        card.End();
        return clicked;
    }

    public static bool TextField(string imguiId, string hint, ref string text, PhoneTheme theme, int maxLength,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None) =>
        TextField(imguiId, hint, ref text, theme, maxLength, flags, out _);

    public static bool TextField(string imguiId, string hint, ref string text, PhoneTheme theme, int maxLength,
        ImGuiInputTextFlags flags, out bool active, float trailingReserve = 0f)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X - trailingReserve;
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        SearchBar.Surface(ImGui.GetWindowDrawList(), field, ControlInk.From(theme));
        var changed = GlassField.Text(field, imguiId, hint, ref text, theme, scale, maxLength, false, flags);
        active = ImGui.IsItemActive();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, field.Height));
        return changed;
    }

    public static bool CodeCard(string value, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var card = GroupCard.Begin(theme, CodeCardHeight);
        var bounds = card.Bounds;
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            Squircle.Fill(ImGui.GetWindowDrawList(), bounds.Min, bounds.Max, Metrics.Radius.Grouped * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, CopiedHoverMix)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var fitted = Typography.FitText(value, bounds.Width - Metrics.Space.Lg * 2f * scale, TextStyles.Title2);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), bounds.Center, fitted, theme.Accent, TextStyles.Title2);
        card.End();
        return UiInteract.Click(bounds.Min, bounds.Max, hovered);
    }

    public static void Steps(PhoneTheme theme, string first, string second, string third, string fourth)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var textWidth = TextWidth(width, scale);
        var firstHeight = StepHeight(first, textWidth, scale);
        var secondHeight = StepHeight(second, textWidth, scale);
        var thirdHeight = StepHeight(third, textWidth, scale);
        var fourthHeight = StepHeight(fourth, textWidth, scale);
        var card = GroupCard.Begin(theme, firstHeight + secondHeight + thirdHeight + fourthHeight);
        card.SeparatorInset = StepBadgeDiameter + Metrics.Space.Md;
        DrawStep(card.NextRow(firstHeight), "1", first, theme, scale);
        DrawStep(card.NextRow(secondHeight), "2", second, theme, scale);
        DrawStep(card.NextRow(thirdHeight), "3", third, theme, scale);
        DrawStep(card.NextRow(fourthHeight), "4", fourth, theme, scale);
        card.End();
    }

    private static float TextWidth(float cardWidth, float scale) =>
        MathF.Max(1f, cardWidth - (Metrics.Space.Lg * 2f + StepBadgeDiameter + Metrics.Space.Md) * scale);

    private static float StepHeight(string text, float textWidth, float scale)
    {
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, textWidth).Y / scale;
        return MathF.Max(GroupCard.DefaultRowHeight, MathF.Max(textHeight, StepBadgeDiameter) + StepRowPadding * 2f);
    }

    private static void DrawStep(Rect row, string number, string text, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var diameter = StepBadgeDiameter * scale;
        var textLeft = row.Min.X + diameter + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, row.Max.X - textLeft);
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, textWidth).Y;
        var top = row.Center.Y - textHeight * 0.5f;
        var badgeCenter = new Vector2(row.Min.X + diameter * 0.5f, row.Center.Y);
        drawList.AddCircleFilled(badgeCenter, diameter * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(theme.TextStrong, FillLevel.Secondary)));
        Typography.DrawCentered(drawList, badgeCenter, number, theme.TextStrong, TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top), text, theme.TextStrong, TextStyles.Subheadline,
            textWidth);
    }
}
