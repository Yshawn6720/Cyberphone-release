using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Notes;

internal sealed partial class NotesApp
{
    private const int FilterCount = 4;
    private const float TileHeight = 78f;
    private const float TileGap = 10f;
    private const float TilePad = 12f;
    private const float TileIconSize = 30f;
    private const float TileGlyphFraction = 0.58f;
    private const float TilesBottomGap = 6f;
    private const float ReminderRowHeight = 58f;
    private const float CheckRadius = 11f;
    private const float CheckStroke = 1.6f;
    private const float CheckHitPad = 8f;
    private const float CheckTextGap = 14f;
    private const float ReminderTitleLift = 9f;
    private const float ReminderDueDrop = 11f;
    private const float CompletedHeaderActionShare = 0.3f;
    private const double LingerSeconds = 1.1;
    private const float EmptyMessageHeight = 220f;
    private const float CheckFillSeconds = 0.18f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private sealed class ReminderRowText
    {
        public long Key = long.MinValue;
        public string Due = string.Empty;
    }

    private readonly ReminderBoard board = new();
    private readonly Dictionary<Guid, ReminderRowText> reminderTexts = new();
    private readonly string[] tileCounts = new string[FilterCount];
    private readonly int[] tileCountValues = { -1, -1, -1, -1 };
    private ReminderFilter reminderFilter;
    private bool showCompleted;
    private Guid lingeringId;
    private double lingerUntil;
    private double completedAt;
    private long boardFingerprint = long.MinValue;
    private string completedHeader = string.Empty;
    private int completedHeaderCount = -1;
    private CultureInfo? completedHeaderCulture;

    private void DrawReminders(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var reminders = configuration.Reminders;
        RefreshBoard(reminders);
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ScrollLayout.StableContentWidth();
            if (reminders.Count == 0)
            {
                if (EmptyState.Draw(VisibleBody(navBar.Body, scale), ui, PhoneIcons.CircleCheck,
                        Loc.T(L.Notes.RemindersEmptyTitle), Loc.T(L.Notes.RemindersEmptyHint),
                        Loc.T(L.Notes.NewReminder)))
                {
                    StartNewReminder();
                }
            }
            else
            {
                DrawFilterTiles(width, scale);
                DrawReminderSections(reminders, width, scale);
            }

            ImGui.Dummy(new Vector2(0f, BottomBreathing * scale));
        }

        AppHeader.EndLargeTitle(in navBar, context, "notes.reminders.nav", Loc.T(L.Notes.TabReminders),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void RefreshBoard(List<ReminderItem> reminders)
    {
        var time = ImGui.GetTime();
        if (lingeringId != Guid.Empty && time >= lingerUntil)
        {
            lingeringId = Guid.Empty;
        }

        var now = DateTime.Now;
        var fingerprint = ReminderFingerprint(reminders, now);
        if (fingerprint == boardFingerprint)
        {
            return;
        }

        boardFingerprint = fingerprint;
        board.Build(reminders, now, reminderFilter, lingeringId);
    }

    private long ReminderFingerprint(List<ReminderItem> reminders, DateTime now)
    {
        var hash = now.Ticks / TimeSpan.TicksPerMinute;
        hash = hash * 31 + (long)reminderFilter;
        hash = hash * 31 + lingeringId.GetHashCode();
        hash = hash * 31 + reminders.Count;
        for (var index = 0; index < reminders.Count; index++)
        {
            var reminder = reminders[index];
            hash = hash * 31 + reminder.Id.GetHashCode();
            hash = hash * 31 + (reminder.Done ? 1 : 0);
            hash = hash * 31 + (reminder.DueAt?.Ticks ?? 0L);
        }

        return hash;
    }

    private void DrawFilterTiles(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var gap = TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = TileHeight * scale;
        for (var index = 0; index < FilterCount; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            DrawFilterTile(new Rect(min, min + new Vector2(tileWidth, tileHeight)), (ReminderFilter)index, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tileHeight * 2f + gap + TilesBottomGap * scale));
    }

    private void DrawFilterTile(Rect rect, ReminderFilter filter, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var tint = FilterTint(filter);
        var active = reminderFilter == filter;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(TileKey(filter), down, PressFx.CardPressedScale);
        var center = rect.Center;
        var half = rect.Size * 0.5f * grow;
        var min = center - half;
        var max = center + half;
        var radius = Metrics.Radius.Grouped * scale;
        if (active)
        {
            IconTile.FillShaded(drawList, min, max, radius, tint);
        }
        else
        {
            ui.Card(drawList, min, max, radius);
        }

        var pad = TilePad * scale;
        var iconSize = TileIconSize * scale;
        var iconCenter = new Vector2(min.X + pad + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f);
        drawList.AddCircleFilled(iconCenter, iconSize * 0.5f, ImGui.GetColorU32(active ? White : tint), 32);
        PhoneIcon.Draw(drawList, iconCenter, FilterGlyph(filter), active ? tint : White, iconSize * TileGlyphFraction);

        var count = TileCount(filter);
        var countSize = Typography.Measure(count, TextStyles.Title1);
        Typography.Draw(drawList, new Vector2(max.X - pad - countSize.X, min.Y + pad * 0.5f), count,
            active ? White : ui.TitleInk, TextStyles.Title1);
        var label = Typography.FitText(FilterLabel(filter), max.X - min.X - pad * 2f, TextStyles.SubheadlineEmphasized);
        var labelHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + pad, max.Y - pad - labelHeight), label,
            active ? White : ui.MutedInk, TextStyles.SubheadlineEmphasized);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return;
        }

        reminderFilter = active && filter != ReminderFilter.All ? ReminderFilter.All : filter;
        boardFingerprint = long.MinValue;
    }

    private static string TileKey(ReminderFilter filter) => filter switch
    {
        ReminderFilter.Today => "notes.tile.today",
        ReminderFilter.Scheduled => "notes.tile.scheduled",
        ReminderFilter.Completed => "notes.tile.completed",
        _ => "notes.tile.all",
    };

    private Vector4 FilterTint(ReminderFilter filter) => filter switch
    {
        ReminderFilter.Today => theme.Accent,
        ReminderFilter.Scheduled => theme.Danger,
        ReminderFilter.Completed => theme.TextMuted,
        _ => ui.Accent,
    };

    private static string FilterGlyph(ReminderFilter filter) => filter switch
    {
        ReminderFilter.Today => PhoneIcons.CalendarFilled,
        ReminderFilter.Scheduled => PhoneIcons.Clock,
        ReminderFilter.Completed => PhoneIcons.Check,
        _ => PhoneIcons.LayoutList,
    };

    private static string FilterLabel(ReminderFilter filter) => filter switch
    {
        ReminderFilter.Today => Loc.T(L.Notes.Today),
        ReminderFilter.Scheduled => Loc.T(L.Notes.FilterScheduled),
        ReminderFilter.Completed => Loc.T(L.Notes.FilterCompleted),
        _ => Loc.T(L.Notes.FilterAll),
    };

    private string TileCount(ReminderFilter filter)
    {
        var value = filter switch
        {
            ReminderFilter.Today => board.CountOf(ReminderGroup.Overdue) + board.CountOf(ReminderGroup.Today),
            ReminderFilter.Scheduled => board.CountOf(ReminderGroup.Overdue) + board.CountOf(ReminderGroup.Today) +
                                        board.CountOf(ReminderGroup.Scheduled),
            ReminderFilter.Completed => board.CountOf(ReminderGroup.Completed),
            _ => board.OpenCount,
        };
        var slot = (int)filter;
        if (tileCountValues[slot] != value || tileCounts[slot] is null)
        {
            tileCountValues[slot] = value;
            tileCounts[slot] = value.ToString(CultureInfo.InvariantCulture);
        }

        return tileCounts[slot];
    }

    private void DrawReminderSections(List<ReminderItem> reminders, float width, float scale)
    {
        var order = board.Order;
        var sections = board.Sections;
        var drewOpen = false;
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var section = sections[sectionIndex];
            var completedInAll = section.Group == ReminderGroup.Completed && reminderFilter == ReminderFilter.All;
            if (completedInAll)
            {
                if (!drewOpen)
                {
                    DrawRemindersMessage(Loc.T(L.Notes.AllDone), Loc.T(L.Notes.AllDoneHint),
                        PhoneIcons.CircleCheckFilled, width, scale);
                }

                DrawCompletedHeader(section.Count, width, scale);
                if (!showCompleted)
                {
                    continue;
                }
            }
            else if (section.Group == ReminderGroup.Completed)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            }
            else
            {
                drewOpen = true;
                DrawGroupHeader(section.Group, width, scale);
            }

            var card = GroupCard.Begin(ui, section.Count, ReminderRowHeight);
            card.SeparatorInset = CheckRadius * 2f + CheckTextGap;
            for (var position = 0; position < section.Count; position++)
            {
                var row = card.NextRow();
                var reminderIndex = order[section.Start + position];
                if (reminderIndex < reminders.Count)
                {
                    DrawReminderRow(card.Bounds, row, reminders[reminderIndex], position == 0,
                        position == section.Count - 1, scale);
                }
            }

            card.End();
        }

        if (order.Count == 0)
        {
            DrawRemindersMessage(FilterEmptyTitle(), string.Empty, FilterGlyph(reminderFilter), width, scale);
        }
    }

    private string FilterEmptyTitle() => reminderFilter switch
    {
        ReminderFilter.Scheduled => Loc.T(L.Notes.ScheduledEmpty),
        ReminderFilter.Completed => Loc.T(L.Notes.CompletedEmpty),
        _ => Loc.T(L.Notes.TodayEmpty),
    };

    private void DrawRemindersMessage(string title, string hint, string glyph, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = EmptyMessageHeight * scale;
        EmptyState.Draw(new Rect(origin, new Vector2(origin.X + width, origin.Y + height)), ui, glyph, title, hint);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawGroupHeader(ReminderGroup group, float width, float scale)
    {
        var title = group switch
        {
            ReminderGroup.Overdue => Loc.T(L.Notes.GroupOverdue),
            ReminderGroup.Today => Loc.T(L.Notes.Today),
            ReminderGroup.Scheduled => Loc.T(L.Notes.FilterScheduled),
            _ => Loc.T(L.Notes.GroupAnytime),
        };
        DrawSectionHeader(title, width, scale, group == ReminderGroup.Overdue ? theme.Danger : ui.TitleInk);
    }

    private void DrawCompletedHeader(int count, float width, float scale)
    {
        if (completedHeaderCount != count || !ReferenceEquals(completedHeaderCulture, Loc.Culture))
        {
            completedHeaderCount = count;
            completedHeaderCulture = Loc.Culture;
            completedHeader = string.Concat(Loc.T(L.Notes.FilterCompleted), "  ",
                count.ToString(CultureInfo.InvariantCulture));
        }

        var origin = ImGui.GetCursorScreenPos();
        var height = SectionHeaderHeight * scale;
        var showLabel = Loc.T(L.Notes.ShowCompleted);
        var hideLabel = Loc.T(L.Notes.HideCompleted);
        var clearLabel = Loc.T(L.Notes.ClearCompleted);
        var actionLimit = width * CompletedHeaderActionShare;
        var toggleWidth = MathF.Min(actionLimit, MathF.Max(Typography.Measure(showLabel, TextStyles.Body).X,
            Typography.Measure(hideLabel, TextStyles.Body).X));
        var clearWidth = MathF.Min(actionLimit, Typography.Measure(clearLabel, TextStyles.Body).X);
        var actionGap = Metrics.Space.Md * scale;
        var drawList = ImGui.GetWindowDrawList();
        var textHeight = Typography.LineHeight(TextStyles.Title3);
        var baseline = origin.Y + height - textHeight - Metrics.Space.Xs * scale;
        var titleWidth = width - toggleWidth - clearWidth - actionGap * 2f - SectionHeaderInset * scale;
        var title = Typography.FitText(completedHeader, MathF.Max(1f, titleWidth), TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(origin.X + SectionHeaderInset * scale, baseline), title, ui.TitleInk,
            TextStyles.Title3);

        var actionTop = baseline;
        var actionHeight = textHeight;
        var toggleRect = new Rect(new Vector2(origin.X + width - toggleWidth, actionTop),
            new Vector2(origin.X + width, actionTop + actionHeight));
        var clearRight = toggleRect.Min.X - actionGap;
        var clearRect = new Rect(new Vector2(clearRight - clearWidth, actionTop),
            new Vector2(clearRight, actionTop + actionHeight));
        if (HeaderLink(drawList, toggleRect, showCompleted ? hideLabel : showLabel))
        {
            showCompleted = !showCompleted;
        }

        if (HeaderLink(drawList, clearRect, clearLabel))
        {
            AskClearCompleted(count);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private bool HeaderLink(ImDrawListPtr drawList, Rect rect, string label)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var fitted = Typography.FitText(label, rect.Width, TextStyles.Body);
        var size = Typography.Measure(fitted, TextStyles.Body);
        var ink = down ? Palette.WithAlpha(ui.Accent, 0.55f) : ui.Accent;
        Typography.Draw(drawList, new Vector2(rect.Max.X - size.X, rect.Center.Y - size.Y * 0.5f), fitted, ink,
            TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawReminderRow(Rect card, Rect row, ReminderItem reminder, bool first, bool last, float scale)
    {
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var radius = CheckRadius * scale;
        var checkCenter = new Vector2(row.Min.X + radius, row.Center.Y);
        var hitPad = radius + CheckHitPad * scale;
        var checkMin = checkCenter - new Vector2(hitPad, hitPad);
        var checkMax = checkCenter + new Vector2(hitPad, hitPad);
        var overCheck = UiInteract.Hover(checkMin, checkMax);
        var washMin = new Vector2(card.Min.X, row.Min.Y);
        var washMax = new Vector2(card.Max.X, row.Max.Y);
        var hovered = !overCheck && UiInteract.Hover(washMin, washMax);
        if (hovered)
        {
            var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            DrawRowWash(drawList, washMin, washMax, first, last, down ? RowPressAlpha : RowWashAlpha, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var fill = reminder.Done ? CheckFill(reminder) : 0f;
        DrawCheckCircle(drawList, checkCenter, radius, fill, overCheck, scale);

        var textLeft = checkCenter.X + radius + CheckTextGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - textLeft);
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        var hasDue = reminder.DueAt.HasValue;
        var titleY = hasDue ? row.Center.Y - ReminderTitleLift * scale - lineHeight * 0.5f
            : row.Center.Y - lineHeight * 0.5f;
        var title = Typography.FitText(reminder.Title.Length > 0 ? reminder.Title : Loc.T(L.Notes.ReminderHint),
            textWidth, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, titleY), title, reminder.Done ? ui.MutedInk : ui.TitleInk,
            TextStyles.Body);
        if (reminder.DueAt is { } due)
        {
            var overdue = !reminder.Done && due < DateTime.Now;
            var dueHeight = Typography.LineHeight(TextStyles.Footnote);
            var dueText = Typography.FitText(DueText(reminder), textWidth, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y + ReminderDueDrop * scale - dueHeight * 0.5f),
                dueText, overdue ? theme.Danger : ui.MutedInk, TextStyles.Footnote);
        }

        if (overCheck)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(checkMin, checkMax, overCheck, false))
            {
                ToggleReminder(reminder);
            }

            return;
        }

        if (UiInteract.Click(washMin, washMax, hovered))
        {
            StartEditReminder(reminder);
        }
    }

    private float CheckFill(ReminderItem reminder)
    {
        if (reminder.Id != lingeringId)
        {
            return 1f;
        }

        return Math.Clamp((float)((ImGui.GetTime() - completedAt) / CheckFillSeconds), 0f, 1f);
    }

    private void DrawCheckCircle(ImDrawListPtr drawList, Vector2 center, float radius, float fill, bool hovered,
        float scale)
    {
        var ring = hovered ? ui.TitleInk : ui.MutedInk;
        if (fill <= 0f)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(ring), 32, CheckStroke * scale);
            return;
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.Mix(ring, ui.Accent, fill)), 32,
            CheckStroke * scale);
        drawList.AddCircleFilled(center, radius * fill, ImGui.GetColorU32(ui.Accent), 32);
        if (fill < 1f)
        {
            return;
        }

        var check = ImGui.GetColorU32(White);
        var thickness = 2f * scale;
        var knee = center + new Vector2(-radius * 0.08f, radius * 0.38f);
        drawList.AddLine(center + new Vector2(-radius * 0.42f, 0f), knee, check, thickness);
        drawList.AddLine(knee, center + new Vector2(radius * 0.46f, -radius * 0.36f), check, thickness);
    }

    private void ToggleReminder(ReminderItem reminder)
    {
        reminder.Done = !reminder.Done;
        if (reminder.Done)
        {
            reminder.Notified = true;
            lingeringId = reminder.Id;
            completedAt = ImGui.GetTime();
            lingerUntil = completedAt + LingerSeconds;
            UiFeedback.Play(UiSound.Success);
        }
        else
        {
            reminder.Notified = reminder.DueAt is not { } due || due <= DateTime.Now;
            if (lingeringId == reminder.Id)
            {
                lingeringId = Guid.Empty;
            }

            UiFeedback.Play(UiSound.ToggleOff);
        }

        boardFingerprint = long.MinValue;
        configuration.Save();
    }

    private string DueText(ReminderItem reminder)
    {
        if (!reminderTexts.TryGetValue(reminder.Id, out var text))
        {
            text = new ReminderRowText();
            reminderTexts[reminder.Id] = text;
        }

        var due = reminder.DueAt ?? default;
        var key = due.Ticks ^ DateTime.Today.Ticks ^ (long)TimeText.FormatVersion << 56 ^
                  Loc.Culture.GetHashCode();
        if (text.Key != key)
        {
            text.Key = key;
            text.Due = DueLabel(due);
        }

        return text.Due;
    }

    private static string DueLabel(DateTime due) =>
        string.Concat(RelativeDay(due.Date), ", ", TimeText.Clock(due));

    private static string RelativeDay(DateTime day)
    {
        var today = DateTime.Today;
        if (day == today)
        {
            return Loc.T(L.Notes.Today);
        }

        if (day == today.AddDays(1))
        {
            return Loc.T(L.Notes.Tomorrow);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Notes.Yesterday);
        }

        return day.ToString(day.Year == today.Year ? "ddd, MMM d" : "d", Loc.Culture);
    }

    private void AskClearCompleted(int count)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.Plural(L.Notes.ClearCompletedConfirm, count),
            ConfirmLabel = Loc.T(L.Notes.ClearCompleted),
            CancelLabel = Loc.T(L.Notes.KeepIt),
            Sheet = true,
            Confirm = ClearCompleted,
        });
    }

    private void ClearCompleted()
    {
        configuration.Reminders.RemoveAll(static reminder => reminder.Done);
        lingeringId = Guid.Empty;
        boardFingerprint = long.MinValue;
        configuration.Save();
    }
}
