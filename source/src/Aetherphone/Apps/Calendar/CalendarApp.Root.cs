using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ParsedEventMap =
    System.Collections.Frozen.FrozenDictionary<long, Aetherphone.Apps.Calendar.ParsedEvent[]>;

namespace Aetherphone.Apps.Calendar;

internal enum CalendarMode : byte
{
    Month,
    Upcoming,
}

internal sealed partial class CalendarApp
{
    private const float ModeStripHeight = 34f;
    private const float ModeStripMaxWidth = 220f;
    private const float ChevronRadius = RoundButton.RegularRadius;

    private const float ChevronGap = 8f;
    private const float SlideFraction = 0.32f;
    private const float StripGap = 14f;
    private const float GridGap = 10f;
    private const float EmptyCardHeight = 76f;
    private const float EmptyTileSize = 40f;
    private const float EmptyGlyphSize = 18f;
    private const float StatusLineHeight = 30f;
    private const float UpcomingStateTop = 48f;
    private const float UpcomingActionWidth = 200f;
    private const int AddButton = 1;
    private const int GroupsButton = 0;

    private readonly NavBarButton[] rootButtons = new NavBarButton[2];
    private readonly string[] modeLabels = new string[2];
    private CalendarMode mode;
    private int monthOffset;
    private DateTime selectedDate;
    private Spring monthSlide;
    private Spring gridHeight;
    private bool gridHeightPrimed;
    private CachedText monthTitle;
    private CachedText selectedTitle;
    private string rootTitle = string.Empty;

    private DateTime VisibleMonth
    {
        get
        {
            var today = DateTime.Today;
            return new DateTime(today.Year, today.Month, 1).AddMonths(monthOffset);
        }
    }

    private void DrawRoot(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var visible = MergedEvents();
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawModeRow(drawList, origin, width, scale);
            cursorY = mode == CalendarMode.Month
                ? DrawMonthMode(drawList, new Vector2(origin.X, cursorY), width, visible, scale)
                : DrawUpcomingMode(drawList, new Vector2(origin.X, cursorY), width, visible, scale);
            CalendarArt.Reserve(origin, width, cursorY + CalendarArt.BottomPad * scale);
        }

        rootTitle = mode == CalendarMode.Month ? MonthTitle(VisibleMonth) : DisplayName;
        rootButtons[GroupsButton] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.LayerGroup), Loc.T(L.Calendar.Groups));
        rootButtons[AddButton] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Plus), Loc.T(L.Calendar.NewEvent));
        UiAnchors.Report("calendar.groups", AppHeader.LargeTitleButtonRect(in navBar, GroupsButton, rootButtons.Length));
        UiAnchors.Report("calendar.new", AppHeader.LargeTitleButtonRect(in navBar, AddButton, rootButtons.Length));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "calendar.nav", rootTitle, NavBarStyle.From(ui),
            rootButtons);
        if (pressed == GroupsButton)
        {
            groupsBackTitle = rootTitle;
            Push(CalendarScreen.Groups);
        }
        else if (pressed == AddButton)
        {
            StartNewEvent(mode == CalendarMode.Month ? selectedDate : DateTime.Today, rootTitle);
        }
    }

    private float DrawModeRow(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var height = ModeStripHeight * scale;
        modeLabels[0] = Loc.T(L.Calendar.ModeMonth);
        modeLabels[1] = Loc.T(L.Calendar.ModeUpcoming);
        var chevronSpan = (ChevronRadius * 4f + ChevronGap) * scale;
        var stripWidth = MathF.Min(ModeStripMaxWidth * scale, width - chevronSpan - Metrics.Space.Md * scale);
        var strip = new Rect(origin, new Vector2(origin.X + stripWidth, origin.Y + height));
        var current = (int)mode;
        var picked = SegmentStrip.Draw("calendar.mode", strip, modeLabels, current,
            Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), ui.Accent, ui.MutedInk, AccentRing.Ink,
            ModeStripHeight, TextStyles.SubheadlineEmphasized.Scale);
        if (picked != current && picked >= 0)
        {
            UiFeedback.Play(UiSound.Tap);
            mode = (CalendarMode)picked;
        }

        if (mode == CalendarMode.Month)
        {
            var radius = ChevronRadius * scale;
            var centerY = origin.Y + height * 0.5f;
            var nextCenter = new Vector2(origin.X + width - radius, centerY);
            var previousCenter = new Vector2(nextCenter.X - radius * 2f - ChevronGap * scale, centerY);
            if (CalendarArt.RoundIcon(drawList, ui, "calendar.previousMonth", previousCenter, radius,
                    FontAwesomeIcon.ChevronLeft, Loc.T(L.Calendar.PreviousMonth), true))
            {
                ShiftMonth(-1);
            }

            if (CalendarArt.RoundIcon(drawList, ui, "calendar.nextMonth", nextCenter, radius,
                    FontAwesomeIcon.ChevronRight, Loc.T(L.Calendar.NextMonth), true))
            {
                ShiftMonth(1);
            }
        }

        return origin.Y + height + StripGap * scale;
    }

    private void ShiftMonth(int delta)
    {
        UiFeedback.Play(UiSound.Tap);
        monthOffset += delta;
        monthSlide.SnapTo(delta > 0 ? 1f : -1f);
        var month = VisibleMonth;
        var today = DateTime.Today;
        selectedDate = month.Year == today.Year && month.Month == today.Month ? today : month;
    }

    private void JumpToToday()
    {
        var delta = -monthOffset;
        monthOffset = 0;
        selectedDate = DateTime.Today;
        if (delta != 0)
        {
            monthSlide.SnapTo(delta > 0 ? 1f : -1f);
        }
    }

    private float DrawMonthMode(ImDrawListPtr drawList, Vector2 origin, float width, ParsedEventMap visible,
        float scale)
    {
        CalendarMonthView.DrawWeekdays(drawList, ui, origin, width, scale);
        var gridTop = origin.Y + CalendarMonthView.WeekdayRowHeight * scale;
        var month = VisibleMonth;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var target = CalendarMonthView.Height(month, false, scale);
        if (!gridHeightPrimed)
        {
            gridHeight.SnapTo(target);
            gridHeightPrimed = true;
        }

        var height = gridHeight.Step(target, Motion.PageSettle, delta);
        var slide = monthSlide.Step(0f, Motion.PageSettle, delta);
        var alpha = Math.Clamp(1f - MathF.Abs(slide), 0f, 1f);
        var clipMax = new Vector2(origin.X + width, gridTop + MathF.Max(height, target));
        drawList.PushClipRect(new Vector2(origin.X, gridTop), clipMax, true);
        var tappedAny = CalendarMonthView.Draw(drawList, ui, new Vector2(origin.X + slide * width * SlideFraction, gridTop),
            width, month, selectedDate, visible, false, alpha, out var tapped, scale);
        drawList.PopClipRect();
        UiAnchors.Report("calendar.grid", new Rect(new Vector2(origin.X, gridTop), clipMax));
        if (tappedAny && tapped != selectedDate)
        {
            UiFeedback.Play(UiSound.Tap);
            selectedDate = tapped;
        }

        return DrawSelectedDay(drawList, new Vector2(origin.X, gridTop + height + GridGap * scale), width, visible,
            scale);
    }

    private float DrawSelectedDay(ImDrawListPtr drawList, Vector2 origin, float width, ParsedEventMap visible,
        float scale)
    {
        var today = DateTime.Today;
        var showToday = selectedDate != today;
        var title = SelectedTitle(selectedDate);
        var headerHeight = CalendarArt.SectionHeader(drawList, ui, origin, width, title,
            showToday ? Loc.T(L.Calendar.Today) : string.Empty, out var todayClicked, scale);
        if (todayClicked)
        {
            JumpToToday();
        }

        agenda.SyncDay(visible, selectedDate);
        var rows = agenda.DayRows;
        var cardTop = origin.Y + headerHeight;
        float bottom;
        if (rows.Count == 0)
        {
            bottom = DrawEmptyDay(drawList, new Vector2(origin.X, cardTop), width, scale);
        }
        else
        {
            bottom = DrawEventRows(drawList, new Vector2(origin.X, cardTop), width, rows, 0, rows.Count, scale);
        }

        UiAnchors.Report("calendar.agenda", new Rect(origin, new Vector2(origin.X + width, bottom)));
        return DrawFeedStatus(drawList, new Vector2(origin.X, bottom), width, scale);
    }

    private float DrawEmptyDay(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + EmptyCardHeight * scale);
        var row = new Rect(origin, max);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var hovered = CalendarArt.RowWash(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = EmptyTileSize * scale;
        var tileMin = new Vector2(origin.X + pad, row.Center.Y - tileSize * 0.5f);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        Squircle.Fill(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.12f)));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, FontAwesomeIcon.Plus, ui.Accent,
            EmptyGlyphSize * scale);
        CalendarArt.Labels(drawList, tileMax.X + CalendarArt.TextGap * scale, max.X - pad, row.Center.Y,
            Loc.T(L.Calendar.NoEvents), Loc.T(L.Calendar.AddEventHint), ui.TitleInk, ui.MutedInk, scale);
        if (UiInteract.Click(origin, max, hovered))
        {
            StartNewEvent(selectedDate, rootTitle);
        }

        return max.Y;
    }

    private float DrawFeedStatus(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (!configuration.CalendarGameEventsInApp || events.IsLoaded)
        {
            return origin.Y;
        }

        if (events.IsLoading)
        {
            var lineHeight = StatusLineHeight * scale;
            Typography.DrawCentered(drawList, new Vector2(origin.X + width * 0.5f, origin.Y + lineHeight * 0.5f +
                    Metrics.Space.Sm * scale), Loc.T(L.Calendar.LoadingGameEvents), ui.MutedInk, TextStyles.Footnote);
            return origin.Y + lineHeight + Metrics.Space.Sm * scale;
        }

        if (!events.HasFailed)
        {
            return origin.Y;
        }

        var top = origin.Y + CalendarArt.CardGap * scale;
        var title = Loc.T(L.Calendar.FailedToLoad);
        var body = Loc.T(L.Calendar.FailedToLoadBody);
        var panelHeight = CalendarArt.PanelHeight(title, body, width, scale);
        var actionHeight = Metrics.Size.TapTarget * scale;
        CalendarArt.Panel(drawList, ui, new Vector2(origin.X, top), width, panelHeight + actionHeight,
            FontAwesomeIcon.ExclamationTriangle, AccentRing.Orange, title, body, scale);
        var actionRow = new Rect(new Vector2(origin.X, top + panelHeight - Metrics.Space.Sm * scale),
            new Vector2(origin.X + width, top + panelHeight + actionHeight - Metrics.Space.Sm * scale));
        if (CalendarArt.TextAction(drawList, actionRow, Loc.T(L.Common.Retry), ui.Accent, scale))
        {
            UiFeedback.Play(UiSound.Refresh);
            events.Retry();
        }

        return top + panelHeight + actionHeight;
    }

    private float DrawUpcomingMode(ImDrawListPtr drawList, Vector2 origin, float width, ParsedEventMap visible,
        float scale)
    {
        agenda.SyncUpcoming(visible);
        var sections = agenda.Sections;
        if (sections.Count == 0)
        {
            var stateBottom = CalendarArt.StateScreen(drawList, ui, origin.X + width * 0.5f,
                origin.Y + UpcomingStateTop * scale, width, FontAwesomeIcon.CalendarCheck,
                Loc.T(L.Calendar.UpcomingEmptyTitle), Loc.T(L.Calendar.UpcomingEmptyBody), scale);
            var actionWidth = MathF.Min(UpcomingActionWidth * scale, width);
            var actionTop = stateBottom + CalendarArt.SectionGap * scale;
            var action = new Rect(new Vector2(origin.X + (width - actionWidth) * 0.5f, actionTop),
                new Vector2(origin.X + (width + actionWidth) * 0.5f, actionTop + Metrics.Size.Pill * scale));
            if (ui.AccentPill(action, Loc.T(L.Calendar.NewEvent), true, TextStyles.Headline))
            {
                StartNewEvent(DateTime.Today, rootTitle);
            }

            return DrawFeedStatus(drawList, new Vector2(origin.X, action.Max.Y), width, scale);
        }

        var rows = agenda.UpcomingRows;
        var cursorY = origin.Y;
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var section = sections[sectionIndex];
            if (sectionIndex > 0)
            {
                cursorY += CalendarArt.CardGap * scale;
            }

            cursorY += CalendarArt.SectionHeader(drawList, ui, new Vector2(origin.X, cursorY), width, section.Title,
                string.Empty, out _, scale);
            cursorY = DrawEventRows(drawList, new Vector2(origin.X, cursorY), width, rows, section.Start,
                section.Count, scale);
        }

        return DrawFeedStatus(drawList, new Vector2(origin.X, cursorY), width, scale);
    }

    private float DrawEventRows(ImDrawListPtr drawList, Vector2 origin, float width, List<AgendaRow> rows, int start,
        int count, float scale)
    {
        var rowHeight = CalendarArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + count * rowHeight);
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        for (var index = 0; index < count; index++)
        {
            var entry = rows[start + index];
            var top = origin.Y + index * rowHeight;
            var row = new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight));
            if (index > 0)
            {
                CalendarArt.Hairline(drawList, ui, origin.X + pad + (CalendarArt.BarWidth + CalendarArt.TextGap) * scale,
                    max.X, top);
            }

            var hovered = CalendarArt.RowWash(drawList, ui, row, scale);
            CalendarArt.Bar(drawList, origin.X + pad, top + CalendarArt.BarInsetY * scale,
                top + rowHeight - CalendarArt.BarInsetY * scale, entry.Event.Color, scale);
            var textLeft = origin.X + pad + (CalendarArt.BarWidth + CalendarArt.TextGap) * scale;
            var trailing = CalendarArt.Trailing(drawList, ui, max.X - pad, row.Center.Y, string.Empty, ui.MutedInk, true,
                scale);
            CalendarArt.Labels(drawList, textLeft, max.X - pad - trailing - Metrics.Space.Sm * scale, row.Center.Y,
                entry.Event.Name, entry.Subtitle, ui.TitleInk, ui.MutedInk, scale);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                OpenEvent(entry.Event, rootTitle);
            }
        }

        return max.Y;
    }

    private string MonthTitle(DateTime month)
    {
        var key = month.Ticks;
        if (monthTitle.IsCurrent(key))
        {
            return monthTitle.Value;
        }

        return monthTitle.Store(key, MonthTitleText(month));
    }

    private string SelectedTitle(DateTime day)
    {
        var key = day.Ticks;
        return selectedTitle.IsCurrent(key) ? selectedTitle.Value : selectedTitle.Store(key, CalendarAgenda.LongDay(day));
    }
}
