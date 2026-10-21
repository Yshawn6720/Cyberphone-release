using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Calendar;

internal static class CalendarArt
{
    public const float RowHeight = 60f;
    public const float FieldRowHeight = 50f;
    public const float SectionHeaderHeight = 40f;
    public const float SectionGap = 22f;
    public const float CardGap = 12f;
    public const float BottomPad = 28f;
    public const float BarWidth = 4f;
    public const float BarInsetY = 12f;
    public const float TextGap = 12f;
    public const float LineGap = 2f;
    public const float ChevronSize = 11f;
    public const float PillHeight = Button.LargeHeight;
    public const float ToggleTileHeight = 74f;

    private const float WashInset = 4f;
    private const float WashRadius = 16f;
    private const float PressedWash = 1.6f;
    private const float StateTileSize = 48f;
    private const float StateGlyphSize = 22f;
    private const float StateGap = 14f;
    private const float StateLineGap = 4f;
    private const float ScreenTileSize = 68f;
    private const float ScreenGlyphSize = 30f;
    private const float ScreenTitleGap = 18f;
    private const float ScreenHintGap = 6f;
    private const float ScreenMaxText = 280f;
    private const float ScreenTextInset = 48f;
    private const float DateTileBandFraction = 0.30f;
    private const float DateTileBandAlpha = 1f;
    private const float ToggleTileGlyph = 16f;
    private const float ToggleTileFillAlpha = 0.14f;
    private const float ToggleTileRestAlpha = 0.06f;
    private const float ToggleTilePad = 12f;
    private const float ToggleCheckRadius = 9f;
    private const float SwatchRingGap = 3f;
    private const float SwatchRingStroke = 2.2f;
    private const float TrailingTextGap = 6f;
    private const float RoundGlyphFraction = 0.8f;
    private const uint StateKeyOffset = 0x51ED27u;

    public static void Hairline(ImDrawListPtr drawList, AppSkin ui, float left, float right, float y) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);

    public static void Reserve(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y - ImGui.GetStyle().ItemSpacing.Y)));
    }

    public static float SectionHeader(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string trailing, out bool trailingClicked, float scale)
    {
        trailingClicked = false;
        var height = SectionHeaderHeight * scale;
        var trailingWidth = trailing.Length > 0 ? Typography.Measure(trailing, TextStyles.Body).X : 0f;
        var reserve = trailingWidth > 0f ? trailingWidth + Metrics.Space.Md * scale : 0f;
        CardSectionHeader.Draw(drawList, origin, width, title, ui.TitleInk, reserve);
        if (trailingWidth <= 0f)
        {
            return height;
        }

        var tapHeight = MathF.Max(height, Metrics.Size.TapTarget * scale);
        var hitMin = new Vector2(origin.X + width - trailingWidth - Metrics.Space.Sm * scale,
            origin.Y + (height - tapHeight) * 0.5f);
        var hitMax = new Vector2(origin.X + width, hitMin.Y + tapHeight);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var ink = hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent;
        var textHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(origin.X + width - trailingWidth, origin.Y + (height - textHeight) * 0.5f),
            trailing, ink, TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        trailingClicked = UiInteract.Click(hitMin, hitMax, hovered);
        if (trailingClicked)
        {
            UiFeedback.Play(UiSound.Tap);
        }

        return height;
    }

    public static bool RoundIcon(ImDrawListPtr drawList, AppSkin ui, string id, Vector2 center, float radius,
        FontAwesomeIcon icon, string tooltip, bool enabled, HoverLabelSide side = HoverLabelSide.Below)
    {
        var clicked = RoundButton.Draw(drawList, ImGui.GetID(id), center, radius, ui.Ink, ButtonStyle.Gray, enabled,
            false, out var face);
        var glyphInk = ui.Ink.AccentInk;
        ProgressRing.CenterIcon(drawList, center, icon, glyphInk with { W = face.LabelInk.W },
            face.Face.Width * 0.5f * RoundGlyphFraction);
        if (enabled && face.Hovered)
        {
            HoverTooltip.Show(new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)),
                tooltip, side);
        }

        return clicked;
    }

    public static bool RowWash(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (!hovered)
        {
            return false;
        }

        var inset = WashInset * scale;
        var wash = ImGui.IsMouseDown(ImGuiMouseButton.Left)
            ? Palette.WithAlpha(ui.HoverTint, ui.HoverTint.W * PressedWash)
            : ui.HoverTint;
        Squircle.Fill(drawList, row.Min + new Vector2(inset, inset), row.Max - new Vector2(inset, inset),
            WashRadius * scale, ImGui.GetColorU32(wash));
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    public static void Bar(ImDrawListPtr drawList, float left, float top, float bottom, Vector4 color, float scale)
    {
        var width = BarWidth * scale;
        drawList.AddRectFilled(new Vector2(left, top), new Vector2(left + width, bottom), ImGui.GetColorU32(color),
            width * 0.5f);
    }

    public static float Trailing(ImDrawListPtr drawList, AppSkin ui, float right, float centerY, string value,
        Vector4 ink, bool chevron, float scale)
    {
        var used = 0f;
        if (chevron)
        {
            var size = ChevronSize * scale;
            ProgressRing.CenterIcon(drawList, new Vector2(right - size * 0.5f, centerY), FontAwesomeIcon.ChevronRight,
                Palette.WithAlpha(ui.MutedInk, ui.MutedInk.W * 0.7f), size);
            used = size + TrailingTextGap * scale;
        }

        if (value.Length == 0)
        {
            return used;
        }

        var textSize = Typography.Measure(value, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(right - used - textSize.X, centerY - textSize.Y * 0.5f), value, ink,
            TextStyles.Body);
        return used + textSize.X;
    }

    public static void Labels(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, Vector4 titleInk, Vector4 subtitleInk, float scale)
    {
        var width = MathF.Max(1f, right - left);
        var fittedTitle = Typography.FitText(title, width, TextStyles.Headline);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), fittedTitle, titleInk,
                TextStyles.Headline);
            return;
        }

        var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + LineGap * scale + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, titleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + LineGap * scale), fittedSubtitle,
            subtitleInk, TextStyles.Footnote);
    }

    public static float PanelHeight(string title, string body, float width, float scale)
    {
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(StateTileSize * scale, titleHeight + StateLineGap * scale + bodyHeight) +
               Metrics.Space.Lg * 2f * scale;
    }

    public static void Panel(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float height,
        FontAwesomeIcon icon, Vector4 tint, string title, string body, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = StateTileSize * scale;
        var tileMin = new Vector2(origin.X + pad, origin.Y + pad);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor, IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);
        var textLeft = tileMax.X + StateGap * scale;
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad), title, ui.TitleInk,
            TextStyles.Headline, textWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad + titleHeight + StateLineGap * scale), body,
            ui.MutedInk, TextStyles.Subheadline, textWidth);
    }

    public static float StateScreen(ImDrawListPtr drawList, AppSkin ui, float centerX, float top, float width,
        FontAwesomeIcon icon, string title, string hint, float scale)
    {
        var tileSize = ScreenTileSize * scale;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, top);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, top + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, ScreenGlyphSize * scale);
        var maxWidth = MathF.Min(width - ScreenTextInset * scale, ScreenMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + ScreenTitleGap * scale), maxWidth);
        return Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
            new Vector2(centerX, titleBottom + ScreenHintGap * scale), maxWidth);
    }

    public static void DateTile(ImDrawListPtr drawList, AppSkin ui, Vector2 min, float size, string month,
        string day, Vector4 color, float scale)
    {
        var max = min + new Vector2(size, size);
        var radius = size * Metrics.Radius.TileFactor;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.Theme.GroupedCard));
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
        var band = size * DateTileBandFraction;
        drawList.PushClipRect(min, new Vector2(max.X, min.Y + band), true);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(color with { W = DateTileBandAlpha }));
        drawList.PopClipRect();
        Typography.DrawCentered(drawList, new Vector2(min.X + size * 0.5f, min.Y + band * 0.5f), month,
            AccentRing.Ink, TextStyles.Caption2);
        Typography.DrawCentered(drawList, new Vector2(min.X + size * 0.5f, min.Y + band + (size - band) * 0.5f), day,
            ui.TitleInk, TextStyles.Title2);
    }

    public static bool ToggleTile(ImDrawListPtr drawList, AppSkin ui, string id, Rect rect, FontAwesomeIcon icon,
        string label, bool on, Vector4 tint, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, Motion.PressScaleCard);
        var fillAmount = PressFx.Toward(ImGui.GetID(id) + StateKeyOffset, on ? 1f : 0f);
        var center = rect.Center;
        var half = rect.Size * 0.5f * press;
        var min = center - half;
        var max = center + half;
        var radius = Metrics.Radius.Card * scale;
        var rest = Palette.WithAlpha(ui.TitleInk, ToggleTileRestAlpha * (hovered ? 1.6f : 1f));
        var active = Palette.WithAlpha(tint, ToggleTileFillAlpha * (hovered ? 1.3f : 1f));
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Vector4.Lerp(rest, active, fillAmount)));
        var pad = ToggleTilePad * scale;
        var glyph = ToggleTileGlyph * scale;
        var ink = Vector4.Lerp(ui.MutedInk, tint, fillAmount);
        ProgressRing.CenterIcon(drawList, new Vector2(min.X + pad + glyph * 0.5f, min.Y + pad + glyph * 0.5f), icon,
            ink, glyph);
        var checkRadius = ToggleCheckRadius * scale;
        var checkCenter = new Vector2(max.X - pad - checkRadius, min.Y + pad + glyph * 0.5f);
        if (fillAmount > 0.01f)
        {
            drawList.AddCircleFilled(checkCenter, checkRadius, ImGui.GetColorU32(tint with { W = fillAmount }), 24);
            ProgressRing.CenterIcon(drawList, checkCenter, FontAwesomeIcon.Check,
                AccentRing.Ink with { W = fillAmount }, checkRadius);
        }

        drawList.AddCircle(checkCenter, checkRadius, ImGui.GetColorU32(ui.MutedInk with { W = 1f - fillAmount }), 24,
            Metrics.Stroke.Thin * scale);
        var labelWidth = max.X - min.X - pad * 2f;
        var labelHeight = Typography.MeasureWrappedBlock(label, TextStyles.FootnoteEmphasized, labelWidth).Y;
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, max.Y - pad * 0.75f - labelHeight), label, ui.TitleInk,
            TextStyles.FootnoteEmphasized, labelWidth);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return false;
        }

        UiFeedback.Play(on ? UiSound.ToggleOff : UiSound.ToggleOn);
        return true;
    }

    public static bool Swatch(ImDrawListPtr drawList, AppSkin ui, string id, Vector2 center, float radius,
        Vector4 color, bool selected, float scale)
    {
        var hit = new Vector2(radius + SwatchRingGap * scale * 2f);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, Motion.PressScaleControl);
        var ring = PressFx.Toward(ImGui.GetID(id) + StateKeyOffset, selected ? 1f : 0f);
        var bodyRadius = radius * press * (1f - 0.18f * ring);
        drawList.AddCircleFilled(center, bodyRadius, ImGui.GetColorU32(color), 32);
        if (ring > 0.01f)
        {
            drawList.AddCircle(center, radius * press + SwatchRingGap * scale * 0.2f,
                ImGui.GetColorU32(color with { W = ring }), 32, SwatchRingStroke * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(center - hit, center + hit, hovered))
        {
            return false;
        }

        UiFeedback.Play(UiSound.Tap);
        return true;
    }

    public static bool TextAction(ImDrawListPtr drawList, Rect row, string label, Vector4 ink, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var fitted = Typography.FitText(label, row.Width - Metrics.Space.Lg * 2f * scale, TextStyles.Body);
        Typography.DrawCentered(drawList, row.Center, fitted, hovered ? Palette.Lighten(ink, 0.12f) : ink,
            TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static float PanelTextWidth(float width, float scale) =>
        MathF.Max(1f, width - Metrics.Space.Lg * 2f * scale - (StateTileSize + StateGap) * scale);
}
