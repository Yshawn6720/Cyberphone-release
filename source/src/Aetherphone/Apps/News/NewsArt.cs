using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.News;

internal static class NewsArt
{
    public const float BannerAspect = 188f / 720f;
    public const float BannerAspectMin = 0.2f;
    public const float BannerAspectMax = 0.75f;
    public const float SectionTopGap = 24f;
    public const float SectionHeaderGap = 4f;
    public const float BarHeight = 5f;
    public const float ChevronSize = 4.5f;

    private const float PlaceholderAlpha = 0.06f;
    private const float SpinnerRadius = 11f;
    private const float SpinnerThickness = 2.4f;
    private const double SpinnerPeriodMs = 900.0;
    private const float SpinnerArc = 1.8f;
    private const float SpinnerAlpha = 0.95f;
    private const float BrokenGlyph = 22f;
    private const float BrokenAlpha = 0.5f;
    private const float StateTile = 72f;
    private const float StateGlyphFraction = 0.46f;
    private const float StateTileToTitle = 20f;
    private const float StateTitleToHint = 6f;
    private const float StateHintToAction = 22f;
    private const float StateActionHeight = Button.RegularHeight;
    private const float StateActionPadding = 44f;
    private const float StateActionMinWidth = 140f;
    private const float StateMaxText = 280f;
    private const float StateSideMargin = 32f;
    private const float StateVerticalBias = 0.42f;
    private const float StateLineSpacing = 1.25f;
    private const float PillPadX = 8f;
    private const float PillPadY = 3f;
    private const float PillFillAlpha = 0.18f;
    private const float BarTrackAlpha = 0.26f;
    private const string StateActionId = "news.state.action";

    public static float BannerHeight(IDalamudTextureWrap? texture, float width)
    {
        if (texture is not null && texture.Size.X > 0f && texture.Size.Y > 0f)
        {
            return width * Math.Clamp(texture.Size.Y / texture.Size.X, BannerAspectMin, BannerAspectMax);
        }

        return width * BannerAspect;
    }

    public static void Banner(ImDrawListPtr drawList, AppSkin ui, IDalamudTextureWrap? texture, bool failed,
        float reveal, Vector2 min, Vector2 max, float radius, ImDrawFlags corners, float scale)
    {
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, PlaceholderAlpha)), radius,
            corners);
        var center = (min + max) * 0.5f;
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, reveal)), radius, corners);
            if (reveal < 1f)
            {
                Spinner(center, ui.MutedInk with { W = ui.MutedInk.W * (1f - reveal) }, scale);
            }

            return;
        }

        if (failed)
        {
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Image,
                Palette.WithAlpha(ui.MutedInk, BrokenAlpha), BrokenGlyph * scale);
            return;
        }

        Spinner(center, ui.MutedInk, scale);
    }

    public static void Spinner(Vector2 center, Vector4 color, float scale) =>
        ProgressRing.Sweep(center, SpinnerRadius * scale, SpinnerThickness * scale, color, SpinnerPeriodMs,
            SpinnerArc, SpinnerAlpha);

    public static void Bar(ImDrawListPtr drawList, Vector2 min, Vector2 max, float fraction, Vector4 ink)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(ink with { W = ink.W * BarTrackAlpha }), radius);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var fillMax = new Vector2(MathF.Max(min.X + radius * 2f, min.X + (max.X - min.X) * clamped), max.Y);
        drawList.AddRectFilled(min, fillMax, ImGui.GetColorU32(ink), radius);
    }

    public static void Chevron(ImDrawListPtr drawList, Vector2 tip, float size, float thickness, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    public static Vector2 PillSize(string label, float scale)
    {
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        return new Vector2(size.X + PillPadX * 2f * scale, size.Y + PillPadY * 2f * scale);
    }

    public static void Pill(ImDrawListPtr drawList, Vector2 min, string label, Vector4 color, float scale)
    {
        var size = PillSize(label, scale);
        var max = min + size;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(color, PillFillAlpha)), size.Y * 0.5f);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, color, TextStyles.FootnoteEmphasized);
    }

    public static Rect Lift(ImDrawListPtr drawList, AppSkin ui, Rect rest, string id, bool hovered, float radius)
    {
        var eased = HoverFx.Amount(id, hovered);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, PressFx.CardPressedScale);
        var factor = (1f + Motion.HoverLiftCard * eased) * press;
        var half = rest.Size * 0.5f * factor;
        var card = new Rect(rest.Center - half, rest.Center + half);
        ui.Card(drawList, card.Min, card.Max, radius * factor);
        return card;
    }

    public static bool StatePanel(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint,
        string action)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var textWidth = MathF.Min(body.Width - StateSideMargin * 2f * scale, StateMaxText * scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Title3, textWidth).Y;
        var hintHeight = hint.Length == 0
            ? 0f
            : StateTitleToHint * scale + Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, textWidth).Y;
        var actionHeight = action.Length == 0 ? 0f : (StateHintToAction + StateActionHeight) * scale;
        var tile = StateTile * scale;
        var total = tile + StateTileToTitle * scale + titleHeight + hintHeight + actionHeight;
        var top = body.Min.Y + MathF.Max(0f, (body.Height - total) * StateVerticalBias);
        var centerX = body.Center.X;
        var tileMin = new Vector2(centerX - tile * 0.5f, top);
        var tileMax = tileMin + new Vector2(tile, tile);
        var radius = tile * Metrics.Radius.TileFactor;
        IconTile.FillShaded(drawList, tileMin, tileMax, radius, IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, tile * StateGlyphFraction);
        var cursorY = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTileToTitle * scale), textWidth, StateLineSpacing);
        if (hint.Length > 0)
        {
            cursorY = Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, cursorY + StateTitleToHint * scale), textWidth, StateLineSpacing);
        }

        if (action.Length == 0)
        {
            return false;
        }

        var natural = Typography.Measure(action, TextStyles.Headline).X + StateActionPadding * scale;
        var width = Math.Clamp(natural, StateActionMinWidth * scale, MathF.Max(StateActionMinWidth * scale, textWidth));
        var actionTop = cursorY + StateHintToAction * scale;
        var rest = new Rect(new Vector2(centerX - width * 0.5f, actionTop),
            new Vector2(centerX + width * 0.5f, actionTop + StateActionHeight * scale));
        return Button.Draw(drawList, rest, action, ui.Ink, id: StateActionId);
    }
}
