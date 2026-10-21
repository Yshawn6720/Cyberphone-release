using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Calendar;

internal sealed partial class CalendarApp
{
    private const float HeroPad = 18f;
    private const float DateTileSize = 60f;
    private const float HeroTextGap = 14f;
    private const float StatusPillHeight = 28f;
    private const float StatusPillPadX = 12f;
    private const float StatusGap = 14f;
    private const float StatusFillAlpha = 0.14f;
    private const float ProgressHeight = 6f;
    private const float ProgressGap = 10f;
    private const float ProgressTrackAlpha = 0.10f;
    private const float InfoRowHeight = 50f;
    private const float DotRadius = 5f;
    private const float DotGap = 8f;
    private const float NotesLabelGap = 6f;
    private const float StateTop = 60f;
    private const int MaxInfoRows = 3;

    private readonly NavBarButton[] detailButtons = new NavBarButton[1];
    private readonly string[] infoLabels = new string[MaxInfoRows];
    private readonly string[] infoValues = new string[MaxInfoRows];
    private readonly bool[] infoDots = new bool[MaxInfoRows];
    private ParsedEvent detailEvent;
    private DateTime detailOccurrence;
    private string detailBackTitle = string.Empty;
    private string detailNotes = string.Empty;
    private string detailDateLine = string.Empty;
    private string detailTimeLine = string.Empty;
    private string detailMonth = string.Empty;
    private string detailDay = string.Empty;
    private bool detailMissing;
    private int infoCount;
    private int detailRevision = -1;
    private CultureInfo? detailCulture;
    private int detailFormat = -1;
    private CachedText detailStatus;

    private void OpenEvent(in ParsedEvent entry, string backTitle)
    {
        detailEvent = entry;
        detailOccurrence = entry.Begin;
        detailBackTitle = backTitle;
        detailRevision = -1;
        detailStatus.Reset();
        Push(CalendarScreen.Event);
    }

    private void SyncDetail()
    {
        if (detailRevision == events.CustomRevision && ReferenceEquals(detailCulture, Loc.Culture) &&
            detailFormat == TimeText.FormatVersion)
        {
            return;
        }

        detailRevision = events.CustomRevision;
        detailCulture = Loc.Culture;
        detailFormat = TimeText.FormatVersion;
        detailStatus.Reset();
        detailMissing = false;
        detailNotes = string.Empty;
        infoCount = 0;
        if (detailEvent.IsCustom)
        {
            var item = FindCustomEvent(detailEvent.CustomId);
            if (item is null)
            {
                detailMissing = true;
                return;
            }

            var group = FindGroup(item.GroupId);
            detailEvent.Name = item.Title;
            detailEvent.Begin = detailOccurrence;
            detailEvent.End = item.EndOf(detailOccurrence);
            detailEvent.Color = CalendarColors.For(group, ui.Accent);
            detailEvent.GroupName = group?.Name ?? string.Empty;
            detailEvent.Repeats = item.Repeat != CalendarRepeat.None;
            detailNotes = item.Notes;
            AddInfo(Loc.T(L.Calendar.Group), group is null ? Loc.T(L.Calendar.NoGroup) : group.Name, true);
            if (item.Repeat != CalendarRepeat.None)
            {
                AddInfo(Loc.T(L.Calendar.Repeat), Loc.T(CalendarReminder.RepeatLabel(item.Repeat)), false);
            }

            AddInfo(Loc.T(L.Calendar.Alert),
                Loc.T(CalendarReminder.LeadLabelAt(CalendarReminder.LeadIndexOf(item.ReminderMinutesBefore))), false);
        }
        else
        {
            AddInfo(Loc.T(L.Calendar.Group), Loc.T(L.Calendar.GameEvents), true);
        }

        var begin = detailEvent.Begin;
        detailMonth = Loc.Culture.TextInfo.ToUpper(begin.ToString("MMM", Loc.Culture));
        detailDay = begin.Day.ToString(CultureInfo.InvariantCulture);
        detailDateLine = detailEvent.SpansDays
            ? string.Concat(begin.ToString(Loc.Culture.DateTimeFormat.MonthDayPattern, Loc.Culture), " – ",
                detailEvent.End.ToString(Loc.Culture.DateTimeFormat.MonthDayPattern, Loc.Culture))
            : CalendarAgenda.LongDay(begin);
        detailTimeLine = CalendarAgenda.Range(detailEvent);
    }

    private void AddInfo(string label, string value, bool dot)
    {
        infoLabels[infoCount] = label;
        infoValues[infoCount] = value;
        infoDots[infoCount] = dot;
        infoCount++;
    }

    private void DrawDetail(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        SyncDetail();
        var scale = UiScale.Current;
        using (AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            float bottom;
            if (detailMissing)
            {
                bottom = CalendarArt.StateScreen(drawList, ui, origin.X + width * 0.5f, origin.Y + StateTop * scale,
                    width, FontAwesomeIcon.CalendarAlt, Loc.T(L.Calendar.EventGoneTitle),
                    Loc.T(L.Calendar.EventGoneBody), scale);
            }
            else
            {
                bottom = DrawDetailBody(drawList, origin, width, scale);
            }

            CalendarArt.Reserve(origin, width, bottom + CalendarArt.BottomPad * scale);
        }

        var editable = detailEvent.IsCustom && !detailMissing;
        var linkable = !detailEvent.IsCustom && !string.IsNullOrEmpty(detailEvent.Url);
        detailButtons[0] = editable
            ? new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Pen), Loc.T(L.Calendar.EditEvent))
            : new NavBarButton(IconGlyph.Of(FontAwesomeIcon.ExternalLinkAlt), Loc.T(L.Calendar.OpenEventPage));
        var count = editable || linkable ? 1 : 0;
        var title = detailMissing ? Loc.T(L.Calendar.Title) : detailEvent.Name;
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "calendar.detail.nav", title, NavBarStyle.From(ui),
            detailButtons.AsSpan(0, count), detailBackTitle, back);
        if (pressed != 0)
        {
            return;
        }

        if (editable)
        {
            StartEditEvent(detailEvent.CustomId, detailEvent.Name);
        }
        else if (linkable)
        {
            UiFeedback.Play(UiSound.Tap);
            Dalamud.Utility.Util.OpenLink(detailEvent.Url);
        }
    }

    private float DrawDetailBody(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var cursorY = DrawHero(drawList, origin, width, scale);
        cursorY = DrawInfo(drawList, new Vector2(origin.X, cursorY + CalendarArt.CardGap * scale), width, scale);
        if (detailNotes.Length > 0)
        {
            cursorY = DrawNotes(drawList, new Vector2(origin.X, cursorY + CalendarArt.CardGap * scale), width, scale);
        }

        if (!detailEvent.IsCustom && !string.IsNullOrEmpty(detailEvent.Url))
        {
            var top = cursorY + CalendarArt.SectionGap * scale;
            var pill = new Rect(new Vector2(origin.X, top),
                new Vector2(origin.X + width, top + CalendarArt.PillHeight * scale));
            if (ui.AccentPill(pill, Loc.T(L.Calendar.OpenEventPage), true, TextStyles.Headline))
            {
                UiFeedback.Play(UiSound.Tap);
                Dalamud.Utility.Util.OpenLink(detailEvent.Url);
            }

            return pill.Max.Y;
        }

        if (!detailEvent.IsCustom)
        {
            return cursorY;
        }

        var deleteTop = cursorY + CalendarArt.SectionGap * scale;
        var deleteRow = new Rect(new Vector2(origin.X, deleteTop),
            new Vector2(origin.X + width, deleteTop + CalendarArt.FieldRowHeight * scale));
        ui.Card(drawList, deleteRow.Min, deleteRow.Max, Metrics.Radius.Grouped * scale);
        var hovered = CalendarArt.RowWash(drawList, ui, deleteRow, scale);
        Typography.DrawCentered(drawList, deleteRow.Center, Loc.T(L.Calendar.DeleteEvent), ui.Theme.Danger,
            TextStyles.Body);
        if (UiInteract.Click(deleteRow.Min, deleteRow.Max, hovered))
        {
            AskDeleteCustomEvent(detailEvent.CustomId, detailEvent.Repeats);
        }

        return deleteRow.Max.Y;
    }

    private float DrawHero(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var now = DateTime.Now;
        var pad = HeroPad * scale;
        var tileSize = DateTileSize * scale;
        var showProgress = detailEvent.SpansDays && detailEvent.Begin <= now && detailEvent.End > now;
        var progressSpan = showProgress ? (ProgressGap + ProgressHeight) * scale : 0f;
        var height = pad * 2f + tileSize + StatusGap * scale + StatusPillHeight * scale + progressSpan;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var tileMin = new Vector2(origin.X + pad, origin.Y + pad);
        CalendarArt.DateTile(drawList, ui, tileMin, tileSize, detailMonth, detailDay, detailEvent.Color, scale);
        var textLeft = tileMin.X + tileSize + HeroTextGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var dateHeight = Typography.LineHeight(TextStyles.Headline);
        var timeHeight = Typography.LineHeight(TextStyles.Subheadline);
        var textTop = tileMin.Y + (tileSize - dateHeight - CalendarArt.LineGap * scale - timeHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(detailDateLine, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + dateHeight + CalendarArt.LineGap * scale),
            Typography.FitText(detailTimeLine, textWidth, TextStyles.Subheadline), ui.MutedInk,
            TextStyles.Subheadline);

        var status = DetailStatus(now, out var live);
        var statusTop = tileMin.Y + tileSize + StatusGap * scale;
        var statusInk = live ? detailEvent.Color : ui.MutedInk;
        var statusWidth = MathF.Min(Typography.Measure(status, TextStyles.SubheadlineEmphasized).X +
                                    StatusPillPadX * 2f * scale, width - pad * 2f);
        var pillMin = new Vector2(origin.X + pad, statusTop);
        var pillMax = new Vector2(pillMin.X + statusWidth, statusTop + StatusPillHeight * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(statusInk, StatusFillAlpha)));
        Typography.DrawCentered(drawList, (pillMin + pillMax) * 0.5f,
            Typography.FitText(status, statusWidth - StatusPillPadX * scale, TextStyles.SubheadlineEmphasized),
            statusInk, TextStyles.SubheadlineEmphasized);
        if (showProgress)
        {
            var total = (detailEvent.End - detailEvent.Begin).TotalSeconds;
            var fraction = total <= 0d ? 1f : (float)((now - detailEvent.Begin).TotalSeconds / total);
            var barTop = pillMax.Y + ProgressGap * scale;
            var barMin = new Vector2(origin.X + pad, barTop);
            var barMax = new Vector2(max.X - pad, barTop + ProgressHeight * scale);
            var radius = (barMax.Y - barMin.Y) * 0.5f;
            drawList.AddRectFilled(barMin, barMax,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, ProgressTrackAlpha)), radius);
            var fillWidth = MathF.Max(barMax.Y - barMin.Y, (barMax.X - barMin.X) * Math.Clamp(fraction, 0f, 1f));
            drawList.AddRectFilled(barMin, new Vector2(barMin.X + fillWidth, barMax.Y),
                ImGui.GetColorU32(detailEvent.Color), radius);
        }

        return max.Y;
    }

    private string DetailStatus(DateTime now, out bool live)
    {
        var end = detailEvent.HasEnd ? detailEvent.End : detailEvent.Begin;
        live = now < end || (!detailEvent.HasEnd && now < detailEvent.Begin.AddMinutes(1));
        var minute = now.Ticks / TimeSpan.TicksPerMinute;
        if (detailStatus.IsCurrent(minute))
        {
            return detailStatus.Value;
        }

        string text;
        if (now < detailEvent.Begin)
        {
            text = Loc.T(L.Calendar.StartsIn, TimeText.Until(detailEvent.Begin - now));
        }
        else if (detailEvent.HasEnd && now < detailEvent.End)
        {
            text = detailEvent.SpansDays
                ? Loc.T(L.Calendar.EndsIn, TimeText.Until(detailEvent.End - now))
                : Loc.T(L.Calendar.HappeningNow);
        }
        else if (!detailEvent.HasEnd && now < detailEvent.Begin.AddMinutes(1))
        {
            text = Loc.T(L.Calendar.HappeningNow);
        }
        else
        {
            text = Loc.T(L.Calendar.Ended);
        }

        return detailStatus.Store(minute, text);
    }

    private float DrawInfo(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rowHeight = InfoRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + infoCount * rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        for (var index = 0; index < infoCount; index++)
        {
            var top = origin.Y + index * rowHeight;
            var centerY = top + rowHeight * 0.5f;
            if (index > 0)
            {
                CalendarArt.Hairline(drawList, ui, origin.X + pad, max.X, top);
            }

            var labelWidth = Typography.Measure(infoLabels[index], TextStyles.Body).X;
            var labelHeight = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(origin.X + pad, centerY - labelHeight * 0.5f), infoLabels[index],
                ui.TitleInk, TextStyles.Body);
            var valueMax = MathF.Max(1f, width - pad * 3f - labelWidth - (infoDots[index] ? (DotRadius * 2f + DotGap) * scale : 0f));
            var value = Typography.FitText(infoValues[index], valueMax, TextStyles.Body);
            var valueWidth = Typography.Measure(value, TextStyles.Body).X;
            var valueLeft = max.X - pad - valueWidth;
            Typography.Draw(drawList, new Vector2(valueLeft, centerY - labelHeight * 0.5f), value, ui.MutedInk,
                TextStyles.Body);
            if (infoDots[index])
            {
                drawList.AddCircleFilled(new Vector2(valueLeft - DotGap * scale - DotRadius * scale, centerY),
                    DotRadius * scale, ImGui.GetColorU32(detailEvent.Color), 20);
            }
        }

        return max.Y;
    }

    private float DrawNotes(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var textWidth = width - pad * 2f;
        var label = Loc.T(L.Calendar.Notes);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var bodyHeight = Typography.MeasureWrappedBlock(detailNotes, TextStyles.Body, textWidth).Y;
        var height = pad * 2f + labelHeight + NotesLabelGap * scale + bodyHeight;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + pad), label, ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad + labelHeight + NotesLabelGap * scale),
            detailNotes, ui.TitleInk, TextStyles.Body, textWidth);
        return max.Y;
    }

    private void AskDeleteCustomEvent(Guid id, bool repeats)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(repeats ? L.Calendar.DeleteRepeatingMessage : L.Calendar.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(L.Calendar.DeleteEvent),
            CancelLabel = Loc.T(L.Calendar.DeleteCancel),
            Sheet = true,
            Confirm = () => DeleteCustomEvent(id),
        });
    }

    private void DeleteCustomEvent(Guid id)
    {
        var customEvents = configuration.CalendarCustomEvents;
        for (var index = customEvents.Count - 1; index >= 0; index--)
        {
            if (customEvents[index].Id == id)
            {
                customEvents.RemoveAt(index);
            }
        }

        SaveCalendar();
        if (router.Current != CalendarScreen.Event)
        {
            return;
        }

        router.Pop();
    }
}
