using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal static class VenuesArt
{
    public const float SectionGap = 22f;
    public const float HeaderGap = 6f;
    public const float CardGap = 12f;
    public const float RowHeight = 68f;
    public const float RowThumb = 48f;
    public const float RowPad = 14f;
    public const float TextGap = 12f;
    public const float LineGap = 2f;
    public const float BottomPad = 24f;
    public const float PillHeight = Button.SmallHeight;
    public const float PillGlyph = 14f;
    public const float PillGap = 8f;
    public const float StateMinHeight = 300f;

    private const float GlyphGap = 6f;
    private const float ChevronGap = 2f;
    private const float ChevronAlpha = 0.8f;
    private const float ChevronFraction = 0.85f;
    private const float WashInset = 4f;
    private const float WashRadius = 16f;
    private const float PressedWash = 1.6f;
    private const float StateTileSize = 68f;
    private const float StateGlyphSize = 30f;
    private const float StateTitleGap = 18f;
    private const float StateHintGap = 6f;
    private const float StateActionGap = 20f;
    private const float StateActionHeight = Button.RegularHeight;
    private const float StateActionPad = 44f;
    private const float StateActionMinWidth = 150f;
    private const float StateMaxText = 280f;
    private const float StateTextInset = 48f;
    private const float StateLift = 40f;

    public static float SectionHeader(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string trailing, out bool trailingClicked, float scale)
    {
        trailingClicked = false;
        var reserve = trailing.Length > 0 ? Typography.Measure(trailing, TextStyles.Body).X + TextGap * scale : 0f;
        var height = CardSectionHeader.Draw(drawList, origin, width, title, ui.TitleInk, reserve);
        if (trailing.Length == 0)
        {
            return height;
        }

        var size = Typography.Measure(trailing, TextStyles.Body);
        var tapHeight = MathF.Max(height, Metrics.Size.TapTarget * scale);
        var hitMin = new Vector2(origin.X + width - size.X - TextGap * scale, origin.Y + (height - tapHeight) * 0.5f);
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

    public static float StateHeight(Rect body, float scale) => MathF.Max(StateMinHeight * scale, body.Height * 0.8f);

    public static bool StateScreen(ImDrawListPtr drawList, AppSkin ui, Rect body, string glyph, string title,
        string hint, string actionLabel, float scale)
    {
        var centerX = body.Center.X;
        var tileSize = StateTileSize * scale;
        var tileTop = body.Center.Y - StateLift * scale - tileSize;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        PhoneIcon.Draw(drawList, (tileMin + tileMax) * 0.5f, glyph, AccentRing.Ink, StateGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - StateTextInset * scale, StateMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), maxWidth);
        var bottom = hint.Length > 0
            ? Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, titleBottom + StateHintGap * scale), maxWidth)
            : titleBottom;
        if (actionLabel.Length == 0)
        {
            return false;
        }

        var natural = Typography.Measure(actionLabel, TextStyles.SubheadlineEmphasized).X + StateActionPad * scale;
        var width = Math.Clamp(natural, StateActionMinWidth * scale, MathF.Max(StateActionMinWidth * scale, maxWidth));
        var top = bottom + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top),
            new Vector2(centerX + width * 0.5f, top + StateActionHeight * scale));
        return Action(drawList, ui, rect, ImGui.GetID("venues.state.action"), actionLabel, string.Empty, false,
            ButtonStyle.Prominent);
    }

    public static float ActionWidth(string label, string glyph, bool chevron, float height)
    {
        var scale = UiScale.Current;
        var width = Typography.Measure(label, Button.LabelStyle(height)).X + height;
        if (glyph.Length > 0)
        {
            width += (PillGlyph + GlyphGap) * scale;
        }

        return chevron ? width + (PillGlyph + ChevronGap) * scale : width;
    }

    public static bool Action(ImDrawListPtr drawList, AppSkin ui, Rect rect, uint key, string label, string glyph,
        bool chevron, ButtonStyle style, bool enabled = true)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink, style, ButtonRole.Normal, enabled, hovered, key);
        if (glyph.Length == 0 && !chevron)
        {
            Button.DrawLabel(drawList, face, label);
        }
        else
        {
            DrawGlyphLabel(drawList, face, label, glyph, chevron);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static void DrawGlyphLabel(ImDrawListPtr drawList, in ButtonFace face, string label, string glyph,
        bool chevron)
    {
        var scale = UiScale.Current;
        var rect = face.Face;
        var style = Button.LabelStyle(rect.Height);
        var glyphSize = glyph.Length > 0 ? PillGlyph * scale : 0f;
        var glyphGap = glyph.Length > 0 ? GlyphGap * scale : 0f;
        var chevronSpan = chevron ? (PillGlyph + ChevronGap) * scale : 0f;
        var labelMax = MathF.Max(1f, rect.Width - rect.Height - glyphSize - glyphGap - chevronSpan);
        var fitted = Typography.FitText(label, labelMax, style);
        var labelSize = Typography.Measure(fitted, style);
        var contentLeft = rect.Center.X - (glyphSize + glyphGap + labelSize.X + chevronSpan) * 0.5f;
        var centerY = rect.Center.Y;
        if (glyphSize > 0f)
        {
            PhoneIcon.Draw(drawList, new Vector2(contentLeft + glyphSize * 0.5f, centerY), glyph, face.LabelInk,
                glyphSize);
        }

        var labelLeft = contentLeft + glyphSize + glyphGap;
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), fitted, face.LabelInk, style);
        if (!chevron)
        {
            return;
        }

        PhoneIcon.Draw(drawList,
            new Vector2(labelLeft + labelSize.X + ChevronGap * scale + PillGlyph * scale * 0.5f, centerY),
            PhoneIcons.ChevronDown, face.LabelInk with { W = face.LabelInk.W * ChevronAlpha },
            PillGlyph * ChevronFraction * scale);
    }

    public static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(1f, bottom - origin.Y)));
    }
}
