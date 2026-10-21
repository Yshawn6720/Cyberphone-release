using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Shortcuts;

internal static class ShortcutsArt
{
    public const float SectionGap = 22f;
    public const float HeaderGap = 4f;
    public const float BottomPad = 28f;
    public const float TileHeight = 104f;
    public const float TileGap = 12f;
    public const float TilePad = 14f;
    public const float TileGlyphSize = 24f;
    public const float TileImage = 30f;
    public const float CornerRadius = 14f;
    public const float CornerHit = 20f;
    public const float RowHeight = 62f;
    public const float IconSize = 36f;
    public const float TextGap = 12f;
    public const float BadgeSize = 32f;
    public const float ActionTileHeight = 76f;
    public const float ActionTileGap = 10f;

    private const float CornerInset = 10f;
    private const float TileNameLines = 2f;
    private const float CornerWashAlpha = 0.22f;
    private const float CornerWashHoverAlpha = 0.34f;
    private const float CornerGlyph = 13f;
    private const float RingThickness = 2.4f;
    private const float RingTrackAlpha = 0.3f;
    private const float StopFraction = 0.34f;
    private const float StatusAlpha = 0.82f;
    private const float WashInset = 4f;
    private const float WashRadius = 14f;
    private const float PressedWash = 1.6f;
    private const float StateTileSize = 64f;
    private const float StateGlyph = 28f;
    private const float StateTitleGap = 16f;
    private const float StateBodyGap = 6f;
    private const float StateActionGap = 18f;
    private const float StateActionHeight = 40f;
    private const float StateActionPad = 40f;
    private const float StateActionMinWidth = 160f;
    private const float StateTextMax = 280f;
    private const float StateTopPad = 36f;
    private const float StateSecondaryGap = 6f;
    private const float ActionGlyphTile = 30f;
    private const float ActionGlyph = 15f;
    private const float ActionLabelGap = 8f;
    private const float BadgeGlyph = 15f;
    private const int MonogramCapacity = 128;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Dictionary<string, string> Monograms = new(StringComparer.Ordinal);

    public static readonly Vector4 CommandTint = new(0.26f, 0.55f, 0.98f, 1f);
    public static readonly Vector4 WaitTint = new(0.96f, 0.58f, 0.20f, 1f);
    public static readonly Vector4 PluginTint = new(0.62f, 0.45f, 0.95f, 1f);
    public static readonly Vector4 LinkTint = new(0.18f, 0.70f, 0.80f, 1f);

    public static Vector4 KindTint(ShortcutStepKind kind) => kind switch
    {
        ShortcutStepKind.Wait => WaitTint,
        ShortcutStepKind.OpenPlugin => PluginTint,
        ShortcutStepKind.OpenUrl => LinkTint,
        _ => CommandTint,
    };

    public static FontAwesomeIcon KindGlyph(ShortcutStepKind kind) => kind switch
    {
        ShortcutStepKind.Wait => FontAwesomeIcon.HourglassHalf,
        ShortcutStepKind.OpenPlugin => FontAwesomeIcon.PuzzlePiece,
        ShortcutStepKind.OpenUrl => FontAwesomeIcon.Globe,
        _ => FontAwesomeIcon.Terminal,
    };

    public static bool RowInteraction(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale, bool enabled = true)
    {
        var hovered = enabled && UiInteract.Hover(row.Min, row.Max);
        if (!hovered)
        {
            return false;
        }

        var inset = WashInset * scale;
        var wash = ImGui.IsMouseDown(ImGuiMouseButton.Left)
            ? Palette.WithAlpha(ui.HoverTint, MathF.Min(1f, ui.HoverTint.W * PressedWash))
            : ui.HoverTint;
        Squircle.Fill(drawList, row.Min + new Vector2(inset, inset), row.Max - new Vector2(inset, inset),
            WashRadius * scale, ImGui.GetColorU32(wash));
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    public static void KindBadge(ImDrawListPtr drawList, Vector2 center, float size, ShortcutStepKind kind)
    {
        var half = new Vector2(size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            IconTile.Surface(KindTint(kind)));
        ProgressRing.CenterIcon(drawList, center, KindGlyph(kind), AccentRing.Ink,
            BadgeGlyph * size / BadgeSize);
    }

    public static void TileBody(ImDrawListPtr drawList, Rect rect, Vector4 tint, float lift, float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        var surface = IconTile.Surface(tint);
        IconTile.FillShaded(drawList, rect.Min, rect.Max, radius,
            lift > 0f ? Palette.Lighten(surface, lift) : surface);
        Material.EdgeSquircle(drawList, rect.Min, rect.Max, radius, scale);
    }

    public static void TileGlyph(ImDrawListPtr drawList, Rect rect, int glyph, string name,
        IDalamudTextureWrap? icon, float scale)
    {
        var pad = TilePad * scale;
        if (icon is not null)
        {
            var size = TileImage * scale;
            var min = new Vector2(rect.Min.X + pad, rect.Min.Y + pad);
            Squircle.FillImage(drawList, min, min + new Vector2(size, size), size * Metrics.Radius.TileFactor,
                icon.Handle, 0xFFFFFFFFu);
            return;
        }

        var glyphSize = TileGlyphSize * scale;
        var center = new Vector2(rect.Min.X + pad + glyphSize * 0.5f, rect.Min.Y + pad + glyphSize * 0.5f);
        if (glyph != 0)
        {
            ProgressRing.CenterIcon(drawList, center, (FontAwesomeIcon)glyph, White, glyphSize);
            return;
        }

        var monogram = Monogram(name);
        var measured = Typography.Measure(monogram, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, center.Y - measured.Y * 0.5f), monogram, White,
            TextStyles.Title3);
    }

    public static void TileName(ImDrawListPtr drawList, Rect rect, string name, string status, float scale)
    {
        var pad = TilePad * scale;
        var width = MathF.Max(1f, rect.Width - pad * 2f);
        var bottom = rect.Max.Y - pad;
        if (status.Length > 0)
        {
            var fitted = Typography.FitText(status, width, TextStyles.Footnote);
            var statusHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(rect.Min.X + pad, bottom - statusHeight), fitted,
                White with { W = StatusAlpha }, TextStyles.Footnote);
            bottom -= statusHeight;
        }

        var maxLines = status.Length > 0 ? 1 : (int)TileNameLines;
        var lines = WidgetText.Clamp(name, TextStyles.Headline, width, maxLines);
        var lineHeight = Typography.LineHeight(TextStyles.Headline);
        WidgetText.Lines(drawList, lines, new Vector2(rect.Min.X + pad, bottom - lines.Length * lineHeight), White,
            TextStyles.Headline, lineHeight);
    }

    public static Vector2 CornerCenter(Rect rect, float scale) =>
        new(rect.Max.X - (CornerInset + CornerRadius) * scale, rect.Min.Y + (CornerInset + CornerRadius) * scale);

    public static bool CornerButton(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon glyph, string tooltip,
        float scale, out bool hovered)
    {
        var hit = new Vector2(CornerHit * scale);
        hovered = UiInteract.Hover(center - hit, center + hit);
        CornerBadge(drawList, center, glyph, scale, hovered);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static void CornerBadge(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon glyph, float scale,
        bool hovered = false)
    {
        drawList.AddCircleFilled(center, CornerRadius * scale,
            ImGui.GetColorU32(White with { W = hovered ? CornerWashHoverAlpha : CornerWashAlpha }), 28);
        ProgressRing.CenterIcon(drawList, center, glyph, White, CornerGlyph * scale);
    }

    public static void RunRing(ImDrawListPtr drawList, Vector2 center, float radius, float progress, Vector4 ink,
        float scale)
    {
        var thickness = RingThickness * scale;
        var ringRadius = radius - thickness * 0.5f;
        ProgressRing.Track(drawList, center, ringRadius, thickness, ink with { W = ink.W * RingTrackAlpha });
        ProgressRing.Fill(drawList, center, ringRadius, thickness, Math.Clamp(progress, 0.06f, 1f), ink);
        var stop = radius * StopFraction;
        drawList.AddRectFilled(center - new Vector2(stop), center + new Vector2(stop), ImGui.GetColorU32(ink),
            1.5f * scale);
    }

    public static bool ActionTile(ImDrawListPtr drawList, AppSkin ui, Rect rect, string id, FontAwesomeIcon glyph,
        string label, Vector4 tint, float scale, bool enabled = true)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, pressed, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * grow;
        var drawn = new Rect(rect.Center - half, rect.Center + half);
        ui.Card(drawList, drawn.Min, drawn.Max, Metrics.Radius.Grouped * scale);
        if (hovered)
        {
            Squircle.Fill(drawList, drawn.Min, drawn.Max, Metrics.Radius.Widget * scale,
                ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var alpha = enabled ? 1f : 0.45f;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var tileSize = ActionGlyphTile * scale;
        var blockHeight = tileSize + ActionLabelGap * scale + labelHeight;
        var top = drawn.Center.Y - blockHeight * 0.5f;
        var tileMin = new Vector2(drawn.Center.X - tileSize * 0.5f, top);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor, IconTile.Surface(tint),
            alpha);
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, glyph, AccentRing.Ink with { W = alpha },
            ActionGlyph * scale);
        var fitted = Typography.FitText(label, drawn.Width - TilePad * scale, TextStyles.FootnoteEmphasized);
        var size = Typography.Measure(fitted, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(drawn.Center.X - size.X * 0.5f, tileMax.Y + ActionLabelGap * scale),
            fitted, ui.TitleInk with { W = ui.TitleInk.W * alpha }, TextStyles.FootnoteEmphasized);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float State(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, FontAwesomeIcon icon,
        string title, string body, string primary, string secondary, out bool primaryClicked,
        out bool secondaryClicked, float scale)
    {
        primaryClicked = false;
        secondaryClicked = false;
        var centerX = origin.X + width * 0.5f;
        var tileSize = StateTileSize * scale;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, origin.Y + StateTopPad * scale);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyph * scale);
        var textWidth = MathF.Min(width, StateTextMax * scale);
        var bottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), textWidth);
        if (body.Length > 0)
        {
            bottom = Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, bottom + StateBodyGap * scale), textWidth);
        }

        if (primary.Length > 0)
        {
            var natural = Typography.Measure(primary, TextStyles.Headline).X + StateActionPad * scale;
            var buttonWidth = Math.Clamp(natural, StateActionMinWidth * scale,
                MathF.Max(StateActionMinWidth * scale, textWidth));
            var top = bottom + StateActionGap * scale;
            var rect = new Rect(new Vector2(centerX - buttonWidth * 0.5f, top),
                new Vector2(centerX + buttonWidth * 0.5f, top + StateActionHeight * scale));
            primaryClicked = ui.AccentPill(rect, primary, true, TextStyles.Headline);
            bottom = rect.Max.Y;
        }

        if (secondary.Length > 0)
        {
            var size = Typography.Measure(secondary, TextStyles.Body);
            var top = bottom + StateSecondaryGap * scale;
            var hitHeight = Metrics.Size.TapTarget * scale;
            var hitMin = new Vector2(centerX - size.X * 0.5f - TilePad * scale, top);
            var hitMax = new Vector2(centerX + size.X * 0.5f + TilePad * scale, top + hitHeight);
            var hovered = UiInteract.Hover(hitMin, hitMax);
            var ink = hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent;
            Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top + (hitHeight - size.Y) * 0.5f),
                secondary, ink, TextStyles.Body);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            secondaryClicked = UiInteract.Click(hitMin, hitMax, hovered);
            bottom = hitMax.Y;
        }

        return bottom;
    }

    public static string Monogram(string name)
    {
        if (Monograms.TryGetValue(name, out var cached))
        {
            return cached;
        }

        if (Monograms.Count >= MonogramCapacity)
        {
            Monograms.Clear();
        }

        cached = ShortcutStore.Monogram(name);
        Monograms[name] = cached;
        return cached;
    }

    public static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }
}
