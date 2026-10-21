using Aetherphone.Core;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp
{
    private const float ChartHeaderHeight = 56f;
    private const float ChartPlotHeight = 140f;
    private const float ChartAxisHeight = 22f;
    private const float RangeStripWidth = 124f;
    private const float RangeStripHeight = 28f;
    private const float WeekBarWidth = 16f;
    private const float MonthBarWidth = 6f;
    private const float ChartHeadroom = 1.08f;
    private const int MonthAxisStep = 10;

    private static readonly string[] RangeStripIds =
    {
        "health.range.water",
        "health.range.steps",
        "health.range.distance",
        "health.range.swimming",
        "health.range.active",
    };

    private readonly int[] chartRanges = new int[HealthSeries.MetricCount];
    private readonly string[] dayRangeLabels = new string[2];
    private readonly float[] chartHeights = new float[HealthSeries.MonthDays];
    private readonly CachedText[] averageValues = new CachedText[HealthSeries.MetricCount];
    private readonly CachedText[] averageUnits = new CachedText[HealthSeries.MetricCount];
    private readonly CachedText[] scrubValues = new CachedText[HealthSeries.MetricCount];

    private float DrawDayChart(ImDrawListPtr drawList, Vector2 origin, float width, HealthMetric metric, double goal,
        float scale)
    {
        var index = (int)metric;
        var pad = HealthArt.CardPad * scale;
        var height = pad * 2f + (ChartHeaderHeight + ChartPlotHeight + ChartAxisHeight) * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var left = origin.X + pad;
        var right = max.X - pad;
        var headerTop = origin.Y + pad;
        var stripWidth = MathF.Min(RangeStripWidth * scale, (right - left) * 0.5f);
        var strip = new Rect(new Vector2(right - stripWidth, headerTop),
            new Vector2(right, headerTop + RangeStripHeight * scale));
        dayRangeLabels[0] = Loc.T(L.Health.RangeWeek);
        dayRangeLabels[1] = Loc.T(L.Health.RangeMonth);
        var selected = SegmentStrip.Draw(RangeStripIds[index], strip, dayRangeLabels, chartRanges[index], ui.Palette,
            RangeStripHeight);
        if (selected != chartRanges[index])
        {
            UiFeedback.Play(UiSound.Tap);
            chartRanges[index] = selected;
        }

        var length = chartRanges[index] == 0 ? HealthSeries.WeekDays : HealthSeries.MonthDays;
        var offset = HealthDigest.Days - length;
        var series = digest.Series(metric, length);
        var recorded = digest.SeriesRecorded(metric, length);
        var peak = Math.Max(HealthSeries.Max(series), goal) * ChartHeadroom;
        var heights = chartHeights.AsSpan(0, length);
        for (var slot = 0; slot < length; slot++)
        {
            heights[slot] = peak > 0d ? (float)(series[slot] / peak) : 0f;
        }

        var plotTop = headerTop + ChartHeaderHeight * scale;
        var plot = new Rect(new Vector2(left, plotTop), new Vector2(right, plotTop + ChartPlotHeight * scale));
        var columnWidth = plot.Width / length;
        var scrub = -1;
        if (UiInteract.Hover(plot.Min, plot.Max))
        {
            scrub = Math.Clamp((int)((ImGui.GetMousePos().X - plot.Min.X) / columnWidth), 0, length - 1);
        }

        var tint = HealthArt.Tint(metric);
        var guide = goal > 0d && peak > 0d ? (float)(goal / peak) : -1f;
        HealthArt.Bars(drawList, plot, heights, recorded, scrub >= 0 ? scrub : length - 1, guide, tint, ui.TitleInk,
            (length == HealthSeries.WeekDays ? WeekBarWidth : MonthBarWidth) * scale, scale);
        var headerWidth = MathF.Max(1f, strip.Min.X - left - HealthArt.TileGap * scale);
        if (scrub >= 0)
        {
            HealthArt.ScrubLine(drawList, plot.Min.X + columnWidth * (scrub + 0.5f), plot.Min.Y, plot.Max.Y,
                ui.TitleInk, scale);
            DrawChartHeader(drawList, left, headerTop, headerWidth, Loc.Upper(digest.DayTitles[offset + scrub]),
                ScrubText(metric, offset + scrub, series[scrub], recorded[scrub]), string.Empty, tint);
        }
        else
        {
            var (averageValue, averageUnit) = AverageText(metric, length, HealthSeries.Average(series, recorded),
                recorded);
            DrawChartHeader(drawList, left, headerTop, headerWidth, Loc.Upper(Loc.T(L.Health.DailyAverage)),
                averageValue, averageUnit, tint);
        }

        DrawAxis(drawList, plot, length, offset, scale);
        return max.Y;
    }

    private void DrawChartHeader(ImDrawListPtr drawList, float left, float top, float width, string label,
        string value, string unit, Vector4 tint)
    {
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(label, width, TextStyles.FootnoteEmphasized),
            ui.MutedInk, TextStyles.FootnoteEmphasized);
        HealthArt.ValueWithUnit(drawList, new Vector2(left, top + Typography.LineHeight(TextStyles.FootnoteEmphasized)),
            width, value, unit, ui.TitleInk, tint, TextStyles.Title2);
    }

    private void DrawAxis(ImDrawListPtr drawList, Rect plot, int length, int offset, float scale)
    {
        var columnWidth = plot.Width / length;
        var labelTop = plot.Max.Y + Metrics.Space.Xs * scale;
        if (length == HealthSeries.WeekDays)
        {
            for (var slot = 0; slot < length; slot++)
            {
                var letter = Typography.FitText(digest.WeekLetters[slot], columnWidth, TextStyles.Footnote);
                var size = Typography.Measure(letter, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(plot.Min.X + columnWidth * (slot + 0.5f) - size.X * 0.5f, labelTop),
                    letter, slot == length - 1 ? ui.TitleInk : ui.MutedInk, TextStyles.Footnote);
            }

            return;
        }

        for (var slot = 0; slot < length; slot += MonthAxisStep)
        {
            var label = digest.ShortDates[offset + slot];
            var size = Typography.Measure(label, TextStyles.Footnote);
            var x = MathF.Min(plot.Min.X + columnWidth * slot, plot.Max.X - size.X);
            Typography.Draw(drawList, new Vector2(x, labelTop), label, ui.MutedInk, TextStyles.Footnote);
        }
    }

    private (string Value, string Unit) AverageText(HealthMetric metric, int length, double average,
        ReadOnlySpan<bool> recorded)
    {
        var index = (int)metric;
        var any = false;
        for (var slot = 0; slot < recorded.Length; slot++)
        {
            any |= recorded[slot];
        }

        var key = any ? (long)Math.Round(average * 10d) * 64L + length * 4L + (long)Units : -1L;
        if (!averageValues[index].IsCurrent(key))
        {
            var (value, unit) = any
                ? HealthText.Metric(metric, average, Units, true)
                : (Loc.T(L.Health.NoData), string.Empty);
            averageValues[index].Store(key, value);
            averageUnits[index].Store(key, unit);
        }

        return (averageValues[index].Value, averageUnits[index].Value);
    }

    private string ScrubText(HealthMetric metric, int slot, double value, bool recorded)
    {
        var index = (int)metric;
        var key = recorded ? ((long)Math.Round(value) << 12) | ((long)slot << 4) | (long)Units : -1L - slot;
        if (scrubValues[index].IsCurrent(key))
        {
            return scrubValues[index].Value;
        }

        var text = recorded ? HealthText.Joined(metric, value, Units, false) : Loc.T(L.Health.NoData);
        return scrubValues[index].Store(key, text);
    }
}
