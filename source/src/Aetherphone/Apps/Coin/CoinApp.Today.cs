using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float RingRadius = 34f;
    private const float RingThickness = 8f;
    private const float RingTrackAlpha = 0.14f;
    private const float ColumnGap = 16f;
    private const float GoalDot = 3.5f;
    private const float ResetGlyph = 10f;
    private const float WeekGap = 14f;
    private const float WeekBarsHeight = 64f;
    private const float WeekBarFraction = 0.46f;
    private const float WeekLabelGap = 6f;
    private const float WeekStubHeight = 3f;
    private const float WeekRestAlpha = 0.45f;
    private const float WeekTrackAlpha = 0.07f;

    private static readonly Vector4 CappedTint = new(0.98f, 0.80f, 0.36f, 1f);

    private readonly long[] weekValues = new long[CoinWeek.Days];
    private CoinLedgerEntryDto[]? weekSource;
    private CoinLedgerEntryDto[]? weekPagedFrom;
    private DateTime weekDay;
    private bool weekEndReached;
    private bool weekCovered;
    private long weekTotal;
    private long weekPeak;
    private string[] dayInitials = Array.Empty<string>();
    private CultureInfo? dayInitialsCulture;
    private Spring capFill;
    private Spring weekGrow;
    private CachedText capText;
    private CachedText resetText;
    private CachedText dailyGoalsText;
    private CachedText weeklyGoalsText;
    private CachedText weekTotalText;

    private void PrimeToday()
    {
        capFill.SnapTo(0f);
        weekGrow.SnapTo(0f);
        weekSource = null;
    }

    private float DrawToday(ImDrawListPtr drawList, Vector2 origin, float width, CoinWalletDto wallet, float scale)
    {
        SyncWeek();
        var top = origin.Y + Metrics.Space.Md * scale;
        var pad = Metrics.Space.Lg * scale;
        var radius = RingRadius * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        CoinGoals.Count(wallet.Rules, false, out var dailyDone, out var dailyTotal);
        CoinGoals.Count(wallet.Rules, true, out var weeklyDone, out var weeklyTotal);
        var goalRows = (dailyTotal > 0 ? 1 : 0) + (weeklyTotal > 0 ? 1 : 0);
        var columnHeight = titleHeight + CoinArt.LineGap * scale + lineHeight +
                           (goalRows + 1) * (footHeight + CoinArt.LineGap * scale) + Metrics.Space.Xs * scale;
        var upperHeight = MathF.Max(radius * 2f, columnHeight);
        var weekHeaderHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var weekHeight = weekHeaderHeight + Metrics.Space.Sm * scale + WeekBarsHeight * scale +
                         WeekLabelGap * scale + footHeight;
        var height = pad + upperHeight + WeekGap * scale * 2f + weekHeight + pad;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + height);
        UiAnchors.Report("coin.today", new Rect(min, max));
        if (!ImGui.IsRectVisible(min, max))
        {
            return max.Y;
        }

        CoinArt.Card(drawList, ui, min, max, scale);
        var capped = wallet.DailyCap > 0 && wallet.EarnedToday >= wallet.DailyCap;
        var goalsComplete = dailyTotal > 0 && dailyDone >= dailyTotal;
        var tint = capped ? CappedTint : ui.Accent;
        var ringCenter = new Vector2(min.X + pad + radius, min.Y + pad + upperHeight * 0.5f);
        var thickness = RingThickness * scale;
        ProgressRing.Track(drawList, ringCenter, radius - thickness * 0.5f, thickness,
            Palette.WithAlpha(tint, RingTrackAlpha));
        var target = capped ? 1f : CoinGoals.Progress(wallet.Rules, false);
        var shown = Math.Clamp(Step(ref capFill, target, Motion.Sheet), 0f, 1f);
        ProgressRing.Fill(drawList, ringCenter, radius - thickness * 0.5f, thickness, shown, tint);
        if (capped || goalsComplete)
        {
            ProgressRing.CenterIcon(drawList, ringCenter, FontAwesomeIcon.Check, tint, radius * 0.62f);
        }
        else
        {
            WidgetText.TabularCentered(drawList, ringCenter, NumberText.Group(wallet.EarnedToday), ui.TitleInk,
                TextStyles.Headline, (radius - thickness) * 1.6f);
        }

        var columnLeft = ringCenter.X + radius + ColumnGap * scale;
        var columnRight = max.X - pad;
        var columnWidth = MathF.Max(1f, columnRight - columnLeft);
        var cursorY = min.Y + pad + (upperHeight - columnHeight) * 0.5f;
        var title = capped ? Loc.T(L.Coin.CapReached) : Loc.T(L.Coin.EarnedToday);
        Typography.Draw(drawList, new Vector2(columnLeft, cursorY),
            Typography.FitText(title, columnWidth, TextStyles.Headline), capped ? tint : ui.TitleInk,
            TextStyles.Headline);
        cursorY += titleHeight + CoinArt.LineGap * scale;
        Typography.Draw(drawList, new Vector2(columnLeft, cursorY),
            Typography.FitText(CapLine(wallet), columnWidth, TextStyles.Subheadline), ui.MutedInk,
            TextStyles.Subheadline);
        cursorY += lineHeight + Metrics.Space.Xs * scale;
        if (dailyTotal > 0)
        {
            GoalLine(drawList, columnLeft, columnRight, cursorY, tint, Loc.T(L.Coin.DailyGoals),
                GoalsText(ref dailyGoalsText, dailyDone, dailyTotal), dailyDone >= dailyTotal, scale);
            cursorY += footHeight + CoinArt.LineGap * scale;
        }

        if (weeklyTotal > 0)
        {
            GoalLine(drawList, columnLeft, columnRight, cursorY, Palette.Lighten(tint, 0.45f),
                Loc.T(L.Coin.WeeklyGoals), GoalsText(ref weeklyGoalsText, weeklyDone, weeklyTotal),
                weeklyDone >= weeklyTotal, scale);
            cursorY += footHeight + CoinArt.LineGap * scale;
        }

        var glyph = ResetGlyph * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(columnLeft + glyph * 0.5f, cursorY + footHeight * 0.5f),
            FontAwesomeIcon.HourglassHalf, ui.MutedInk, glyph);
        var resetLeft = columnLeft + glyph + Metrics.Space.Xs * scale;
        Typography.Draw(drawList, new Vector2(resetLeft, cursorY),
            Typography.FitText(ResetLine(wallet.ResetsAtUnix), MathF.Max(1f, columnRight - resetLeft),
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var dividerY = min.Y + pad + upperHeight + WeekGap * scale;
        CoinArt.Hairline(drawList, ui, min.X + pad, max.X - pad, dividerY);
        DrawWeekChart(drawList, new Rect(new Vector2(min.X + pad, dividerY + WeekGap * scale),
            new Vector2(max.X - pad, max.Y - pad)), weekHeaderHeight, footHeight, scale);
        return max.Y;
    }

    private void DrawWeekChart(ImDrawListPtr drawList, Rect area, float headerHeight, float labelHeight, float scale)
    {
        Typography.Draw(drawList, area.Min, Loc.T(L.Coin.WeekTitle), ui.MutedInk, TextStyles.SubheadlineEmphasized);
        if (weekCovered)
        {
            var total = WeekTotalLine();
            var totalWidth = CoinArt.PriceWidth(total, TextStyles.SubheadlineEmphasized);
            CoinArt.Price(drawList, new Vector2(area.Max.X - totalWidth, area.Min.Y), total, ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
        }

        var grow = Math.Clamp(Step(ref weekGrow, weekCovered ? 1f : 0f, Motion.PageSettle), 0f, 1f);
        var barsTop = area.Min.Y + headerHeight + Metrics.Space.Sm * scale;
        var barsBottom = barsTop + WeekBarsHeight * scale;
        var slot = area.Width / CoinWeek.Days;
        var barWidth = slot * WeekBarFraction;
        var radius = MathF.Min(barWidth * 0.5f, 6f * scale);
        var initials = DayInitials();
        var today = DateTime.Now.Date;
        var start = CoinWeek.WindowStart(today);
        for (var dayIndex = 0; dayIndex < CoinWeek.Days; dayIndex++)
        {
            var centerX = area.Min.X + slot * (dayIndex + 0.5f);
            var barMin = new Vector2(centerX - barWidth * 0.5f, barsTop);
            var barMax = new Vector2(centerX + barWidth * 0.5f, barsBottom);
            drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, WeekTrackAlpha)),
                radius);
            var isToday = dayIndex == CoinWeek.Days - 1;
            var value = weekValues[dayIndex];
            if (weekCovered && weekPeak > 0)
            {
                var fraction = (float)((double)value / weekPeak) * grow;
                var barHeight = value > 0
                    ? MathF.Max(radius * 2f, (barsBottom - barsTop) * fraction)
                    : WeekStubHeight * scale;
                var fill = isToday ? ui.Accent : Palette.WithAlpha(ui.Accent, WeekRestAlpha);
                drawList.AddRectFilled(new Vector2(barMin.X, barsBottom - barHeight), barMax,
                    ImGui.GetColorU32(fill), MathF.Min(radius, barHeight * 0.5f));
            }

            var date = start.AddDays(dayIndex);
            var label = initials.Length == 7 ? initials[(int)date.DayOfWeek] : string.Empty;
            if (label.Length == 0)
            {
                continue;
            }

            var labelSize = Typography.Measure(label, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(centerX - labelSize.X * 0.5f, barsBottom + WeekLabelGap * scale),
                label, isToday ? ui.TitleInk : ui.MutedInk, TextStyles.Footnote);
        }
    }

    private void SyncWeek()
    {
        if (!store.LoadedOnce)
        {
            weekSource = null;
            weekCovered = false;
            return;
        }

        var entries = store.Entries;
        var today = DateTime.Now.Date;
        var endReached = store.EndReached;
        if (!ReferenceEquals(entries, weekSource) || today != weekDay || endReached != weekEndReached)
        {
            weekSource = entries;
            weekDay = today;
            weekEndReached = endReached;
            weekCovered = CoinWeek.Covered(entries, endReached, today, TimeZoneInfo.Local);
            weekTotal = CoinWeek.Fill(entries, today, TimeZoneInfo.Local, weekValues);
            weekPeak = CoinWeek.Peak(weekValues);
        }

        if (weekCovered || store.LoadingMore || ReferenceEquals(entries, weekPagedFrom))
        {
            return;
        }

        weekPagedFrom = entries;
        store.LoadMore();
    }

    private string[] DayInitials()
    {
        if (ReferenceEquals(dayInitialsCulture, Loc.Culture))
        {
            return dayInitials;
        }

        dayInitialsCulture = Loc.Culture;
        dayInitials = Loc.Culture.DateTimeFormat.ShortestDayNames;
        return dayInitials;
    }

    private void GoalLine(ImDrawListPtr drawList, float left, float right, float top, Vector4 dot, string label,
        string value, bool complete, float scale)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var dotRadius = GoalDot * scale;
        drawList.AddCircleFilled(new Vector2(left + dotRadius, top + lineHeight * 0.5f), dotRadius,
            ImGui.GetColorU32(dot), 16);
        var valueSize = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        var labelLeft = left + dotRadius * 2f + Metrics.Space.Sm * scale;
        var labelWidth = MathF.Max(1f, right - valueSize.X - Metrics.Space.Sm * scale - labelLeft);
        Typography.Draw(drawList, new Vector2(labelLeft, top), Typography.FitText(label, labelWidth, TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(right - valueSize.X, top), value, complete ? dot : ui.TitleInk,
            TextStyles.FootnoteEmphasized);
    }

    private string CapLine(CoinWalletDto wallet)
    {
        var key = (wallet.EarnedToday << 1) ^ (wallet.DailyCap << 32);
        return capText.IsCurrent(key)
            ? capText.Value
            : capText.Store(key, Loc.T(L.Coin.CapProgress, NumberText.Group(wallet.EarnedToday),
                NumberText.Group(wallet.DailyCap)));
    }

    private string ResetLine(long resetsAtUnix)
    {
        var remainingMinutes = Math.Max(0L, (resetsAtUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / 60L);
        var key = (resetsAtUnix << 20) ^ remainingMinutes;
        return resetText.IsCurrent(key)
            ? resetText.Value
            : resetText.Store(key, Loc.T(L.Coin.CapResets, TimeText.Until(resetsAtUnix)));
    }

    private static string GoalsText(ref CachedText cache, int done, int total)
    {
        var key = ((long)done << 16) | (uint)total;
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, Loc.T(L.Coin.GoalsDone, done, total));
    }

    private string WeekTotalLine()
    {
        return weekTotalText.IsCurrent(weekTotal)
            ? weekTotalText.Value
            : weekTotalText.Store(weekTotal, NumberText.Group(weekTotal));
    }

    private static float Step(ref Spring spring, float target, float smoothTime)
    {
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        return spring.Step(target, smoothTime, deltaSeconds);
    }
}
