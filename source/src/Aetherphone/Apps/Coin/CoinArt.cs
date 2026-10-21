using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal enum CapsuleTone : byte
{
    Filled,
    Tinted,
    Quiet,
}

internal static class CoinArt
{
    public const float RowHeight = 62f;
    public const float SectionHeaderHeight = CardSectionHeader.HeightUnits;
    public const float SectionGap = 22f;
    public const float HeaderGap = 6f;
    public const float BottomPad = 28f;
    public const float RowTile = 36f;
    public const float TextGap = 12f;
    public const float ValueGap = 10f;
    public const float LineGap = 2f;
    public const float CapsuleHeight = Button.RegularHeight;
    public const float LargeCapsuleHeight = Button.LargeHeight;

    public static readonly Vector4 GainInk = new(0.30f, 0.84f, 0.46f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);

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
    private const float ScreenActionGap = 22f;
    private const float ScreenMaxText = 280f;
    private const float ScreenTextInset = 48f;
    private const float ScreenLift = 36f;
    private const float ScreenActionWidth = 180f;

    public static void Card(ImDrawListPtr drawList, AppSkin ui, Vector2 min, Vector2 max, float scale) =>
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);

    public static float SectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title, Vector4 ink,
        float trailingReserve, float scale) =>
        CardSectionHeader.Draw(drawList, origin, width, title, ink, trailingReserve);

    public static void Reserve(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    public static float SectionTitle(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string trailing, out bool trailingClicked, float scale)
    {
        trailingClicked = false;
        var reserve = trailing.Length > 0
            ? Typography.Measure(trailing, TextStyles.Body).X + ValueGap * scale
            : 0f;
        var height = SectionHeader(drawList, origin, width, title, ui.TitleInk, reserve, scale);
        if (trailing.Length == 0)
        {
            return height;
        }

        var size = Typography.Measure(trailing, TextStyles.Body);
        var tapHeight = MathF.Max(height, Metrics.Size.TapTarget * scale);
        var hitMin = new Vector2(origin.X + width - size.X - ValueGap * scale, origin.Y + (height - tapHeight) * 0.5f);
        var hitMax = new Vector2(origin.X + width, hitMin.Y + tapHeight);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var ink = hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent;
        Typography.Draw(drawList, new Vector2(origin.X + width - size.X, origin.Y + (height - size.Y) * 0.5f),
            trailing, ink, TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        trailingClicked = UiInteract.Click(hitMin, hitMax, hovered);
        return height;
    }

    public static bool RowInteraction(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale)
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

    public static void Hairline(ImDrawListPtr drawList, AppSkin ui, float left, float right, float y) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);

    public static void Labels(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, Vector4 titleInk, Vector4 subtitleInk, float scale)
    {
        var width = MathF.Max(1f, right - left);
        var fittedTitle = Typography.FitText(title, width, TextStyles.BodyEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), fittedTitle, titleInk,
                TextStyles.BodyEmphasized);
            return;
        }

        var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + LineGap * scale + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, titleInk, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + LineGap * scale), fittedSubtitle,
            subtitleInk, TextStyles.Footnote);
    }

    public static float Value(ImDrawListPtr drawList, float right, float centerY, string value, Vector4 ink)
    {
        if (value.Length == 0)
        {
            return 0f;
        }

        var width = WidgetText.TabularWidth(value, TextStyles.Headline);
        var height = Typography.LineHeight(TextStyles.Headline);
        WidgetText.Tabular(drawList, new Vector2(right - width, centerY - height * 0.5f), value, ink,
            TextStyles.Headline);
        return width;
    }

    public static float Price(ImDrawListPtr drawList, Vector2 topLeft, string amount, Vector4 ink, in TextStyle style)
    {
        var lineHeight = Typography.LineHeight(style);
        var glyph = lineHeight * CurrencyGlyph.GlyphFraction;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, new Vector2(topLeft.X + glyph * 0.5f,
            topLeft.Y + lineHeight * 0.5f), glyph);
        var reserve = CurrencyGlyph.Reserve(lineHeight);
        var width = WidgetText.Tabular(drawList, new Vector2(topLeft.X + reserve, topLeft.Y), amount, ink, style);
        return reserve + width;
    }

    public static float PriceWidth(string amount, in TextStyle style) =>
        CurrencyGlyph.Reserve(Typography.LineHeight(style)) + WidgetText.TabularWidth(amount, style);

    public static void Bar(ImDrawListPtr drawList, Vector2 min, Vector2 max, float fraction, Vector4 track,
        Vector4 fill)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(track), radius);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var width = MathF.Max(max.Y - min.Y, (max.X - min.X) * clamped);
        drawList.AddRectFilled(min, new Vector2(min.X + width, max.Y), ImGui.GetColorU32(fill), radius);
    }

    public static bool Capsule(ImDrawListPtr drawList, AppSkin ui, uint id, Rect rect, string label,
        CapsuleTone tone, bool enabled)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var style = tone switch
        {
            CapsuleTone.Filled => ButtonStyle.Prominent,
            CapsuleTone.Tinted => ButtonStyle.Tinted,
            _ => ButtonStyle.Gray,
        };
        var face = Button.Surface(drawList, rect, ui.Ink, style, ButtonRole.Normal, enabled, hovered, id);
        Button.DrawLabel(drawList, face, label);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static bool PriceCapsule(ImDrawListPtr drawList, AppSkin ui, uint id, Rect rect, string amount,
        bool enabled)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink, ButtonStyle.Tinted, ButtonRole.Normal, enabled, hovered, id);
        var style = Button.LabelStyle(face.Face.Height);
        var width = PriceWidth(amount, style);
        var lineHeight = Typography.LineHeight(style);
        Price(drawList, new Vector2(face.Face.Center.X - width * 0.5f, face.Face.Center.Y - lineHeight * 0.5f), amount,
            face.LabelInk, style);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float CapsuleWidth(string label, float height) =>
        Typography.Measure(label, Button.LabelStyle(height)).X + height;

    public static float PanelHeight(string title, string body, float width, float scale)
    {
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth).Y;
        var bodyHeight = body.Length == 0 ? 0f : Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(StateTileSize * scale, titleHeight + StateLineGap * scale + bodyHeight) +
               Metrics.Space.Lg * 2f * scale;
    }

    public static void Panel(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float height,
        FontAwesomeIcon icon, Vector4 tint, string title, string body, float scale)
    {
        Card(drawList, ui, origin, new Vector2(origin.X + width, origin.Y + height), scale);
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
        if (body.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad + titleHeight + StateLineGap * scale),
                body, ui.MutedInk, TextStyles.Subheadline, textWidth);
        }
    }

    public static float DrawPanel(AppSkin ui, Vector2 origin, float width, FontAwesomeIcon icon, Vector4 tint,
        string title, string body, float scale)
    {
        var height = PanelHeight(title, body, width, scale);
        Panel(ImGui.GetWindowDrawList(), ui, origin, width, height, icon, tint, title, body, scale);
        return origin.Y + height;
    }

    public static bool StateScreen(ImDrawListPtr drawList, AppSkin ui, Rect body, FontAwesomeIcon icon, string title,
        string hint, string action, uint actionId, float scale)
    {
        var centerX = body.Center.X;
        var tileSize = ScreenTileSize * scale;
        var tileTop = body.Center.Y - ScreenLift * scale - tileSize;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, ScreenGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - ScreenTextInset * scale, ScreenMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + ScreenTitleGap * scale), maxWidth);
        var hintBottom = Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
            new Vector2(centerX, titleBottom + ScreenHintGap * scale), maxWidth);
        if (action.Length == 0)
        {
            return false;
        }

        var height = LargeCapsuleHeight * scale;
        var width = MathF.Min(maxWidth, MathF.Max(ScreenActionWidth * scale, CapsuleWidth(action, height)));
        var top = hintBottom + ScreenActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top), new Vector2(centerX + width * 0.5f, top + height));
        return Capsule(drawList, ui, actionId, rect, action, CapsuleTone.Filled, true);
    }

    private static float PanelTextWidth(float width, float scale) =>
        MathF.Max(1f, width - Metrics.Space.Lg * 2f * scale - (StateTileSize + StateGap) * scale);
}
