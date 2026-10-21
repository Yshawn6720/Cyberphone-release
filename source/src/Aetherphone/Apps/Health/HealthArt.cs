using Aetherphone.Apps.Market;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Health;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health;

internal static class HealthArt
{
    public const float SectionGap = 24f;
    public const float HeaderGap = 10f;
    public const float CardPad = 16f;
    public const float TileGap = 12f;
    public const float LineGap = 2f;
    public const float BottomBreathing = 28f;

    private const float StateTileSize = 44f;
    private const float StateGap = 14f;
    private const float StateLineGap = 3f;
    private const float ChevronThickness = 1.8f;
    private const float GlyphFraction = 0.46f;
    private const float VesselRadiusFraction = 0.16f;
    private const float VesselStroke = 1.6f;
    private const float VesselGlassAlpha = 0.07f;
    private const float VesselRimAlpha = 0.38f;
    private const float VesselGlossAlpha = 0.16f;
    private const float WaveAmplitude = 2.4f;
    private const float WaveCycles = 1.3f;
    private const double WavePeriodMs = 2400.0;
    private const int WaveColumns = 28;
    private const float WaterTopLighten = 0.22f;
    private const float WaterCrestAlpha = 0.55f;
    private const float BarTrackAlpha = 0.10f;
    private const float BarRestAlpha = 0.55f;
    private const float GuideAlpha = 0.45f;
    private const float LineWidth = 2.2f;
    private const float DotRadius = 3.4f;
    private const float FillAlpha = 0.22f;

    public static readonly Vector4 WeightTint = AccentRing.Indigo;
    public static readonly Vector4 EnergyTint = AppPalettes.HealthEnergy;
    public static readonly Vector4 TeleportTint = AppPalettes.HealthTeleport;

    private static readonly Vector4[] MetricTints =
    {
        AppPalettes.HealthWater,
        AccentRing.Orange,
        AccentRing.Red,
        AccentRing.Azure,
        AccentRing.Green,
    };

    private static readonly FontAwesomeIcon[] MetricIcons =
    {
        FontAwesomeIcon.Tint,
        FontAwesomeIcon.ShoePrints,
        FontAwesomeIcon.Route,
        FontAwesomeIcon.Swimmer,
        FontAwesomeIcon.Stopwatch,
    };

    public static Vector4 Tint(HealthMetric metric) => MetricTints[(int)metric];

    public static FontAwesomeIcon Icon(HealthMetric metric) => MetricIcons[(int)metric];

    public static float FrameDelta() => MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);

    public static void GlyphTile(ImDrawListPtr drawList, Vector2 center, float size, Vector4 tint,
        FontAwesomeIcon icon)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, center, icon, AccentRing.Ink, size * GlyphFraction);
    }

    public static void Chevron(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = ChevronThickness * scale;
        var tip = new Vector2(center.X + size * 0.5f, center.Y);
        drawList.AddLine(new Vector2(center.X - size * 0.5f, center.Y - size), tip, color, thickness);
        drawList.AddLine(new Vector2(center.X - size * 0.5f, center.Y + size), tip, color, thickness);
    }

    public static float StateHeight(string title, string body, float width, float scale)
    {
        var textWidth = StateTextWidth(width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(StateTileSize * scale, titleHeight + StateLineGap * scale + bodyHeight) +
               CardPad * 2f * scale;
    }

    public static float State(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, FontAwesomeIcon icon,
        Vector4 tint, string title, string body, float scale)
    {
        var height = StateHeight(title, body, width, scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = CardPad * scale;
        var tileSize = StateTileSize * scale;
        GlyphTile(drawList, new Vector2(origin.X + pad + tileSize * 0.5f, origin.Y + pad + tileSize * 0.5f), tileSize,
            tint, icon);
        var textLeft = origin.X + pad + tileSize + StateGap * scale;
        var textWidth = StateTextWidth(width, scale);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad), title, ui.TitleInk,
            TextStyles.Headline, textWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad + titleHeight + StateLineGap * scale), body,
            ui.MutedInk, TextStyles.Subheadline, textWidth);
        return height;
    }

    private static float StateTextWidth(float width, float scale) =>
        MathF.Max(1f, width - (CardPad * 2f + StateTileSize + StateGap) * scale);

    public static void ValueWithUnit(ImDrawListPtr drawList, Vector2 topLeft, float width, string value, string unit,
        Vector4 valueInk, Vector4 unitInk, in TextStyle valueStyle)
    {
        var valueText = Typography.FitText(value, width, valueStyle);
        var valueSize = Typography.Measure(valueText, valueStyle);
        Typography.Draw(drawList, topLeft, valueText, valueInk, valueStyle);
        var unitWidth = width - valueSize.X - LineGap * 2f;
        if (unit.Length == 0 || unitWidth <= 0f)
        {
            return;
        }

        var unitText = Typography.FitText(unit, unitWidth, TextStyles.FootnoteEmphasized);
        var unitHeight = Typography.Measure(unitText, TextStyles.FootnoteEmphasized).Y;
        Typography.Draw(drawList,
            new Vector2(topLeft.X + valueSize.X + LineGap * 2f, topLeft.Y + valueSize.Y - unitHeight - LineGap),
            unitText, unitInk, TextStyles.FootnoteEmphasized);
    }

    public static void Vessel(ImDrawListPtr drawList, Rect rect, float fraction, Vector4 tint, Vector4 ink,
        float scale)
    {
        var radius = rect.Width * VesselRadiusFraction;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(ink, VesselGlassAlpha)));
        var level = Math.Clamp(fraction, 0f, 1f);
        if (level > 0.001f)
        {
            DrawWater(drawList, rect, radius, level, tint, scale);
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ink, VesselRimAlpha)), VesselStroke * scale);
        var glossLeft = rect.Min.X + rect.Width * 0.16f;
        drawList.AddLine(new Vector2(glossLeft, rect.Min.Y + radius), new Vector2(glossLeft, rect.Max.Y - radius * 1.6f),
            ImGui.GetColorU32(Palette.WithAlpha(ink, VesselGlossAlpha)), VesselStroke * 1.6f * scale);
    }

    private static void DrawWater(ImDrawListPtr drawList, Rect rect, float radius, float level, Vector4 tint,
        float scale)
    {
        var inset = VesselStroke * scale;
        var left = rect.Min.X + inset;
        var right = rect.Max.X - inset;
        var bottom = rect.Max.Y - inset;
        var innerRadius = MathF.Max(0f, radius - inset);
        var height = bottom - (rect.Min.Y + inset);
        var amplitude = WaveAmplitude * scale * MathF.Min(1f, level * 6f) * MathF.Min(1f, (1f - level) * 8f + 0.3f);
        var surface = bottom - height * level;
        var phase = Pulse.Phase(WavePeriodMs) * MathF.Tau;
        var columnWidth = (right - left) / WaveColumns;
        var topColor = ImGui.GetColorU32(Palette.Lighten(tint, WaterTopLighten));
        var bottomColor = ImGui.GetColorU32(tint);
        var firstVertex = drawList.VtxBuffer.Size;
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        var previousTop = Wave(left, left, right, surface, amplitude, phase);
        var previousBottom = Floor(left, left, right, bottom, innerRadius);
        Span<Vector2> crest = stackalloc Vector2[WaveColumns + 1];
        crest[0] = new Vector2(left, MathF.Min(previousTop, previousBottom));
        for (var column = 1; column <= WaveColumns; column++)
        {
            var x = left + columnWidth * column;
            var top = Wave(x, left, right, surface, amplitude, phase);
            var floor = Floor(x, left, right, bottom, innerRadius);
            var previousX = x - columnWidth;
            drawList.PathLineTo(new Vector2(previousX, MathF.Min(previousTop, previousBottom)));
            drawList.PathLineTo(new Vector2(x, MathF.Min(top, floor)));
            drawList.PathLineTo(new Vector2(x, floor));
            drawList.PathLineTo(new Vector2(previousX, previousBottom));
            drawList.PathFillConvex(bottomColor);
            crest[column] = new Vector2(x, MathF.Min(top, floor));
            previousTop = top;
            previousBottom = floor;
        }

        drawList.Flags = flags;
        var vertices = drawList.VtxBuffer.AsSpan();
        var span = MathF.Max(1f, bottom - surface + amplitude);
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            var depth = Math.Clamp((vertices[index].Pos.Y - (surface - amplitude)) / span, 0f, 1f);
            vertices[index].Col = depth < 0.5f ? topColor : bottomColor;
        }

        var crestColor = ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(tint, 0.45f), WaterCrestAlpha));
        for (var column = 0; column < WaveColumns; column++)
        {
            drawList.AddLine(crest[column], crest[column + 1], crestColor, 1.2f * scale);
        }
    }

    private static float Wave(float x, float left, float right, float surface, float amplitude, float phase)
    {
        var t = (x - left) / MathF.Max(1f, right - left);
        return surface + amplitude * MathF.Sin(t * MathF.Tau * WaveCycles + phase);
    }

    private static float Floor(float x, float left, float right, float bottom, float radius)
    {
        if (radius <= 0f)
        {
            return bottom;
        }

        var fromEdge = MathF.Min(x - left, right - x);
        if (fromEdge >= radius)
        {
            return bottom;
        }

        var offset = radius - fromEdge;
        return bottom - radius + MathF.Sqrt(MathF.Max(0f, radius * radius - offset * offset));
    }

    public static void Bars(ImDrawListPtr drawList, Rect area, ReadOnlySpan<float> heights,
        ReadOnlySpan<bool> recorded, int highlight, float guide, Vector4 tint, Vector4 guideInk, float barWidth,
        float scale)
    {
        var count = heights.Length;
        if (count == 0)
        {
            return;
        }

        var columnWidth = area.Width / count;
        var width = MathF.Min(barWidth, columnWidth * 0.7f);
        var rounding = width * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var centerX = area.Min.X + columnWidth * (index + 0.5f);
            var left = centerX - width * 0.5f;
            var right = centerX + width * 0.5f;
            drawList.AddRectFilled(new Vector2(left, area.Min.Y), new Vector2(right, area.Max.Y),
                ImGui.GetColorU32(Palette.WithAlpha(tint, BarTrackAlpha)), rounding);
            var fraction = Math.Clamp(heights[index], 0f, 1f);
            if (!recorded[index] || fraction <= 0.001f)
            {
                continue;
            }

            var top = MathF.Min(area.Max.Y - width, area.Max.Y - area.Height * fraction);
            var alpha = highlight < 0 || highlight == index ? 1f : BarRestAlpha;
            drawList.AddRectFilled(new Vector2(left, top), new Vector2(right, area.Max.Y),
                ImGui.GetColorU32(Palette.WithAlpha(tint, alpha)), rounding);
        }

        if (guide is > 0f and <= 1f)
        {
            MarketArt.DashedLine(drawList, area.Min.X, area.Max.X, area.Max.Y - area.Height * guide,
                Palette.WithAlpha(guideInk, GuideAlpha), scale);
        }
    }

    public static void Line(ImDrawListPtr drawList, Rect area, ReadOnlySpan<float> values, ReadOnlySpan<bool> present,
        float low, float high, Vector4 tint, int scrubIndex, float scale)
    {
        var count = values.Length;
        if (count == 0)
        {
            return;
        }

        var span = MathF.Max(0.001f, high - low);
        var stepX = count > 1 ? area.Width / (count - 1) : 0f;
        Span<Vector2> points = stackalloc Vector2[count];
        var filled = 0;
        for (var index = 0; index < count; index++)
        {
            if (!present[index])
            {
                continue;
            }

            var x = count > 1 ? area.Min.X + stepX * index : area.Center.X;
            var y = area.Max.Y - (values[index] - low) / span * area.Height;
            points[filled++] = new Vector2(x, y);
        }

        if (filled == 0)
        {
            return;
        }

        MarketArt.GradientFill(drawList, points[..filled], area.Max.Y, Palette.WithAlpha(tint, FillAlpha),
            Palette.WithAlpha(tint, 0f));
        var lineColor = ImGui.GetColorU32(tint);
        for (var index = 0; index < filled - 1; index++)
        {
            drawList.AddLine(points[index], points[index + 1], lineColor, LineWidth * scale);
        }

        for (var index = 0; index < filled; index++)
        {
            drawList.AddCircleFilled(points[index], DotRadius * scale, lineColor, 12);
        }

        if (scrubIndex < 0 || scrubIndex >= count || !present[scrubIndex])
        {
            return;
        }

        var scrubX = count > 1 ? area.Min.X + stepX * scrubIndex : area.Center.X;
        var scrubY = area.Max.Y - (values[scrubIndex] - low) / span * area.Height;
        drawList.AddCircleFilled(new Vector2(scrubX, scrubY), DotRadius * 1.9f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.35f)), 16);
        drawList.AddCircleFilled(new Vector2(scrubX, scrubY), DotRadius * 1.2f * scale, lineColor, 16);
    }

    public static void ScrubLine(ImDrawListPtr drawList, float x, float top, float bottom, Vector4 ink, float scale)
    {
        drawList.AddLine(new Vector2(x, top), new Vector2(x, bottom), ImGui.GetColorU32(Palette.WithAlpha(ink, 0.4f)),
            Metrics.Stroke.Thin * scale);
    }
}
