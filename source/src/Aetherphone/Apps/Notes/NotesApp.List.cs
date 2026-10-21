using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Notes;

internal sealed partial class NotesApp
{
    private const int SearchMaxLength = 80;
    private const float NoteRowHeight = 64f;
    private const float RowTitleTop = 12f;
    private const float RowLineTop = 36f;
    private const float StampGap = 8f;
    private const float SectionHeaderHeight = CardSectionHeader.HeightUnits;
    private const float SectionHeaderInset = 4f;
    private const float SearchGap = 6f;
    private const float TrashRowHeight = 52f;
    private const float TrashTileSize = 30f;
    private const float TrashTileGap = 12f;
    private const float ChevronSize = 7f;
    private const float FooterHeight = 44f;
    private const float BottomBreathing = 12f;
    private const float RowWashAlpha = 0.06f;
    private const float RowPressAlpha = 0.11f;
    private const float LongPressSeconds = 0.45f;
    private const float LongPressSlop = 6f;
    private const int RecentDayWindow = 7;

    private sealed class NoteRowText
    {
        public string Body = string.Empty;
        public long StampKey = -1;
        public string Title = string.Empty;
        public string Preview = string.Empty;
        public string Stamp = string.Empty;
    }

    private readonly NoteLibrary library = new();
    private readonly Dictionary<Guid, NoteRowText> rowTexts = new();
    private readonly Dictionary<int, string> yearLabels = new();
    private string searchQuery = string.Empty;
    private string builtQuery = string.Empty;
    private long builtFingerprint = long.MinValue;
    private DateTime builtDay;
    private int countLabelValue = -1;
    private CultureInfo? countLabelCulture;
    private string countLabel = string.Empty;
    private string noResultsQuery = string.Empty;
    private string noResultsLabel = string.Empty;
    private bool longPressFired;
    private int trashCountValue = -1;
    private string trashCountText = string.Empty;

    private void DrawNotes(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var notes = configuration.Notes;
        RefreshLibrary(notes);
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ScrollLayout.StableContentWidth();
            if (notes.Count == 0)
            {
                DrawNotesEmpty(VisibleBody(navBar.Body, scale), scale);
            }
            else
            {
                DrawSearch(width, scale);
                DrawNoteSections(notes, width, scale);
            }

            ImGui.Dummy(new Vector2(0f, BottomBreathing * scale));
        }

        AppHeader.EndLargeTitle(in navBar, context, "notes.nav", Loc.T(L.Notes.TabNotes), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private static Rect VisibleBody(Rect body, float scale) =>
        new(body.Min, new Vector2(body.Max.X, MathF.Max(body.Min.Y + 1f, body.Max.Y - TabBar.ContentInset(scale))));

    private void RefreshLibrary(List<PhoneNote> notes)
    {
        var query = searchQuery.AsSpan().Trim();
        var fingerprint = NoteLibrary.Fingerprint(notes);
        var today = DateTime.Today;
        if (fingerprint == builtFingerprint && today == builtDay &&
            query.Equals(builtQuery, StringComparison.Ordinal))
        {
            return;
        }

        builtFingerprint = fingerprint;
        builtDay = today;
        builtQuery = query.ToString();
        library.Build(notes, today, builtQuery);
    }

    private void DrawNotesEmpty(Rect body, float scale)
    {
        if (configuration.RecentlyDeletedNotes.Count > 0)
        {
            DrawTrashEntry(ScrollLayout.StableContentWidth(), scale);
        }

        if (EmptyState.Draw(body, ui, PhoneIcons.FileText, Loc.T(L.Notes.EmptyTitle), Loc.T(L.Notes.EmptyHint),
                Loc.T(L.Notes.NewNote)))
        {
            StartNewNote();
        }
    }

    private void DrawSearch(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        var drawList = ImGui.GetWindowDrawList();
        SearchBar.Surface(drawList, field, ui.Ink);
        GlassField.Search(drawList, field, "##notesSearch", Loc.T(L.Notes.SearchHint), ref searchQuery, theme, scale,
            SearchMaxLength, false);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, field.Height + SearchGap * scale));
    }

    private void DrawNoteSections(List<PhoneNote> notes, float width, float scale)
    {
        var order = library.Order;
        var sections = library.Sections;
        if (order.Count == 0)
        {
            DrawNoResults(width, scale);
            return;
        }

        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var section = sections[sectionIndex];
            DrawSectionHeader(SectionTitle(section), width, scale);
            var card = GroupCard.Begin(ui, section.Count, NoteRowHeight);
            for (var position = 0; position < section.Count; position++)
            {
                var row = card.NextRow();
                var noteIndex = order[section.Start + position];
                if (noteIndex < notes.Count)
                {
                    DrawNoteRow(card.Bounds, row, notes[noteIndex], position == 0, position == section.Count - 1,
                        scale);
                }
            }

            card.End();
        }

        if (builtQuery.Length == 0 && configuration.RecentlyDeletedNotes.Count > 0)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            DrawTrashEntry(width, scale);
        }

        DrawFooterCount(builtQuery.Length > 0 ? library.MatchCount : notes.Count, width, scale);
    }

    private void DrawSectionHeader(string title, float width, float scale) =>
        DrawSectionHeader(title, width, scale, ui.TitleInk);

    private void DrawSectionHeader(string title, float width, float scale, Vector4 ink)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = CardSectionHeader.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X + SectionHeaderInset * scale, origin.Y), width - SectionHeaderInset * scale, title,
            ink);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private string SectionTitle(in NoteSection section) => section.Bucket switch
    {
        NoteBucket.Pinned => Loc.T(L.Notes.SectionPinned),
        NoteBucket.Today => Loc.T(L.Notes.Today),
        NoteBucket.Yesterday => Loc.T(L.Notes.Yesterday),
        NoteBucket.PreviousWeek => Loc.T(L.Notes.SectionPreviousWeek),
        NoteBucket.PreviousMonth => Loc.T(L.Notes.SectionPreviousMonth),
        NoteBucket.Month => Loc.Culture.DateTimeFormat.GetMonthName(section.Month),
        _ => YearLabel(section.Year),
    };

    private string YearLabel(int year)
    {
        if (yearLabels.TryGetValue(year, out var label))
        {
            return label;
        }

        label = year.ToString(CultureInfo.InvariantCulture);
        yearLabels[year] = label;
        return label;
    }

    private void DrawNoteRow(Rect card, Rect row, PhoneNote note, bool first, bool last, float scale)
    {
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var washMin = new Vector2(card.Min.X, row.Min.Y);
        var washMax = new Vector2(card.Max.X, row.Max.Y);
        var hovered = UiInteract.Hover(washMin, washMax);
        if (hovered)
        {
            var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            DrawRowWash(drawList, washMin, washMax, first, last, down ? RowPressAlpha : RowWashAlpha, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var text = RowTextFor(note);
        var hasTitle = text.Title.Length > 0;
        var title = Typography.FitText(hasTitle ? text.Title : Loc.T(L.Notes.Untitled), row.Width, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Min.Y + RowTitleTop * scale), title,
            hasTitle ? ui.TitleInk : ui.MutedInk, TextStyles.Headline);

        var lineY = row.Min.Y + RowLineTop * scale;
        var stampWidth = Typography.Measure(text.Stamp, TextStyles.Subheadline).X;
        Typography.Draw(drawList, new Vector2(row.Min.X, lineY), text.Stamp, ui.BodyInk, TextStyles.Subheadline);
        var previewLeft = row.Min.X + stampWidth + StampGap * scale;
        var previewWidth = row.Max.X - previewLeft;
        if (previewWidth > 0f)
        {
            var previewSource = text.Preview.Length > 0 ? text.Preview : Loc.T(L.Notes.NoAdditionalText);
            var preview = Typography.FitText(previewSource, previewWidth, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(previewLeft, lineY), preview, ui.MutedInk, TextStyles.Subheadline);
        }

        if (RowGesture(washMin, washMax, hovered))
        {
            StartEditNote(note);
        }
        else if (LongPressed(hovered))
        {
            OpenNoteSheet(note, false);
        }
    }

    private bool RowGesture(Vector2 min, Vector2 max, bool hovered)
    {
        if (!hovered)
        {
            return false;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            longPressFired = false;
        }

        return !longPressFired && UiInteract.Click(min, max, hovered);
    }

    private bool LongPressed(bool hovered)
    {
        if (!hovered || noteSheet.IsOpen)
        {
            return false;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            return true;
        }

        var io = ImGui.GetIO();
        var slop = LongPressSlop * UiScale.Current;
        if (longPressFired || !ImGui.IsMouseDown(ImGuiMouseButton.Left) || io.MouseDownDuration[0] < LongPressSeconds ||
            io.MouseDragMaxDistanceSqr[0] > slop * slop)
        {
            return false;
        }

        longPressFired = true;
        UiInteract.CancelPendingTap();
        return true;
    }

    private void DrawRowWash(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool first, bool last, float alpha,
        float scale)
    {
        var color = ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, alpha));
        var radius = Metrics.Radius.Grouped * scale;
        if (first && last)
        {
            Squircle.Fill(drawList, min, max, radius, color);
            return;
        }

        if (first || last)
        {
            Squircle.FillCap(drawList, min, max, radius, color, first);
            return;
        }

        drawList.AddRectFilled(min, max, color);
    }

    private NoteRowText RowTextFor(PhoneNote note)
    {
        if (!rowTexts.TryGetValue(note.Id, out var text))
        {
            text = new NoteRowText();
            rowTexts[note.Id] = text;
        }

        if (!ReferenceEquals(text.Body, note.Body))
        {
            text.Body = note.Body;
            text.Title = note.Title();
            text.Preview = note.Preview();
        }

        var today = DateTime.Today;
        var stampKey = note.UpdatedAt.Ticks / TimeSpan.TicksPerMinute ^ today.Ticks ^ (long)TimeText.FormatVersion << 56;
        if (text.StampKey != stampKey)
        {
            text.StampKey = stampKey;
            text.Stamp = ListStamp(note.UpdatedAt, today);
        }

        return text;
    }

    private static string ListStamp(DateTime updatedAt, DateTime today)
    {
        var day = updatedAt.Date;
        if (day >= today)
        {
            return TimeText.Clock(updatedAt);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Notes.Yesterday);
        }

        return day > today.AddDays(-RecentDayWindow)
            ? Loc.Culture.DateTimeFormat.GetDayName(day.DayOfWeek)
            : day.ToString("d", Loc.Culture);
    }

    private void DrawNoResults(float width, float scale)
    {
        if (!string.Equals(noResultsQuery, builtQuery, StringComparison.Ordinal) || noResultsLabel.Length == 0)
        {
            noResultsQuery = builtQuery;
            noResultsLabel = Loc.T(L.Notes.NoResultsHint, builtQuery);
        }

        var origin = ImGui.GetCursorScreenPos();
        var height = ImGui.GetContentRegionAvail().Y;
        var area = new Rect(origin, new Vector2(origin.X + width, origin.Y + MathF.Max(height, 1f)));
        EmptyState.Draw(area, ui, PhoneIcons.Search, Loc.T(L.Notes.NoResultsTitle), noResultsLabel);
    }

    private void DrawTrashEntry(float width, float scale)
    {
        var card = GroupCard.Begin(ui, 1, TrashRowHeight);
        var row = card.NextRow();
        var bounds = card.Bounds;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            DrawRowWash(drawList, bounds.Min, bounds.Max, true, true, down ? RowPressAlpha : RowWashAlpha, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tile = TrashTileSize * scale;
        var tileMin = new Vector2(row.Min.X, row.Center.Y - tile * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            ui.MutedInk);
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tile, tile) * 0.5f, PhoneIcons.Trash,
            new Vector4(1f, 1f, 1f, 1f), tile * 0.6f);

        var count = TrashCountLabel(configuration.RecentlyDeletedNotes.Count);
        var countWidth = Typography.Measure(count, TextStyles.Body).X;
        var chevronX = row.Max.X - ChevronSize * scale;
        var countX = chevronX - Metrics.Space.Md * scale - countWidth;
        var labelLeft = tileMin.X + tile + TrashTileGap * scale;
        var label = Typography.FitText(Loc.T(L.Notes.RecentlyDeleted), MathF.Max(1f, countX - labelLeft - 8f * scale),
            TextStyles.Body);
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - lineHeight * 0.5f), label, ui.TitleInk,
            TextStyles.Body);
        Typography.Draw(drawList, new Vector2(countX, row.Center.Y - lineHeight * 0.5f), count, ui.MutedInk,
            TextStyles.Body);
        DrawChevron(drawList, new Vector2(chevronX, row.Center.Y), ui.MutedInk, scale);
        card.End();
        if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
        {
            router.Push(NotesScreen.RecentlyDeleted);
        }
    }

    private string TrashCountLabel(int count)
    {
        if (trashCountValue != count)
        {
            trashCountValue = count;
            trashCountText = count.ToString(Loc.Culture);
        }

        return trashCountText;
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        var half = ChevronSize * scale * 0.5f;
        var stroke = ImGui.GetColorU32(color);
        var thickness = Metrics.Stroke.Ring * scale;
        drawList.AddLine(new Vector2(center.X - half * 0.5f, center.Y - half), new Vector2(center.X + half * 0.5f,
            center.Y), stroke, thickness);
        drawList.AddLine(new Vector2(center.X + half * 0.5f, center.Y), new Vector2(center.X - half * 0.5f,
            center.Y + half), stroke, thickness);
    }

    private void DrawFooterCount(int count, float width, float scale)
    {
        if (countLabelValue != count || !ReferenceEquals(countLabelCulture, Loc.Culture))
        {
            countLabelValue = count;
            countLabelCulture = Loc.Culture;
            countLabel = Loc.Plural(L.Notes.NoteCount, count);
        }

        var origin = ImGui.GetCursorScreenPos();
        var height = FooterHeight * scale;
        Typography.DrawCentered(ImGui.GetWindowDrawList(), new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f),
            countLabel, ui.MutedInk, TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }
}
