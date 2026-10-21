using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Notes;

internal sealed partial class NotesApp
{
    private const float TrashHintGap = 10f;

    private readonly ActionSheet trashSheet = new();
    private readonly ActionSheet.Item[] trashSheetItems = new ActionSheet.Item[2];
    private readonly NavBarButton[] trashButtons = new NavBarButton[1];
    private readonly Dictionary<Guid, string> daysLeftLabels = new();
    private readonly Action closeTrash;
    private PhoneNote? trashSheetNote;
    private DateTime daysLeftDay;
    private System.Globalization.CultureInfo? daysLeftCulture;
    private string trashHint = string.Empty;
    private System.Globalization.CultureInfo? trashHintCulture;

    private void DrawTrash(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var trash = configuration.RecentlyDeletedNotes;
        var now = DateTime.Now;
        if (now.Date != daysLeftDay || !ReferenceEquals(daysLeftCulture, Loc.Culture))
        {
            daysLeftDay = now.Date;
            daysLeftCulture = Loc.Culture;
            daysLeftLabels.Clear();
        }

        using (AppSurface.Begin(navBar.Body))
        {
            var width = ScrollLayout.StableContentWidth();
            if (trash.Count == 0)
            {
                EmptyState.Draw(navBar.Body, ui, PhoneIcons.Trash, Loc.T(L.Notes.TrashEmptyTitle),
                    Loc.T(L.Notes.TrashEmptyHint));
            }
            else
            {
                DrawTrashHint(width, scale);
                DrawTrashRows(trash, now, width, scale);
            }

            ImGui.Dummy(new Vector2(0f, BottomBreathing * scale));
        }

        trashButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.TrashAlt), Loc.T(L.Notes.DeleteAll));
        var buttons = trash.Count > 0 ? trashButtons.AsSpan() : Span<NavBarButton>.Empty;
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "notes.trash.nav", Loc.T(L.Notes.RecentlyDeleted),
            NavBarStyle.From(ui), buttons, Loc.T(L.Notes.TabNotes), closeTrash);
        if (pressed == 0)
        {
            AskDeleteAll();
        }
    }

    private void DrawTrashHint(float width, float scale)
    {
        if (!ReferenceEquals(trashHintCulture, Loc.Culture) || trashHint.Length == 0)
        {
            trashHintCulture = Loc.Culture;
            trashHint = Loc.T(L.Notes.TrashHint, NoteTrash.RetentionDays);
        }

        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(origin, trashHint, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + TrashHintGap * scale));
    }

    private void DrawTrashRows(List<PhoneNote> trash, DateTime now, float width, float scale)
    {
        var card = GroupCard.Begin(ui, trash.Count, NoteRowHeight);
        for (var index = 0; index < trash.Count; index++)
        {
            var row = card.NextRow();
            DrawTrashRow(card.Bounds, row, trash[index], now, index == 0, index == trash.Count - 1, scale);
        }

        card.End();
    }

    private void DrawTrashRow(Rect card, Rect row, PhoneNote note, DateTime now, bool first, bool last, float scale)
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
        var title = Typography.FitText(text.Title.Length > 0 ? text.Title : Loc.T(L.Notes.Untitled), row.Width,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Min.Y + RowTitleTop * scale), title, ui.TitleInk,
            TextStyles.Headline);
        var left = DaysLeftLabel(note, now);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Min.Y + RowLineTop * scale),
            Typography.FitText(left, row.Width, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        if (UiInteract.Click(washMin, washMax, hovered) ||
            (hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right)))
        {
            OpenTrashSheet(note);
        }
    }

    private string DaysLeftLabel(PhoneNote note, DateTime now)
    {
        if (daysLeftLabels.TryGetValue(note.Id, out var label))
        {
            return label;
        }

        label = Loc.Plural(L.Notes.DaysLeft, Math.Max(1, NoteTrash.DaysLeft(note, now)));
        daysLeftLabels[note.Id] = label;
        return label;
    }

    private void OpenTrashSheet(PhoneNote note)
    {
        trashSheetNote = note;
        trashSheetItems[0] = new ActionSheet.Item(Loc.T(L.Notes.Recover), PhoneIcons.ArrowBackUp);
        trashSheetItems[1] = new ActionSheet.Item(Loc.T(L.Notes.DeleteNow), PhoneIcons.Trash, true);
        trashSheet.Open();
    }

    private void DrawTrashSheet(Rect screen)
    {
        if (!trashSheet.CapturesPointer)
        {
            return;
        }

        if (router.Current != NotesScreen.RecentlyDeleted)
        {
            trashSheet.Close();
        }

        var title = trashSheetNote is null ? string.Empty : NoteTitle(trashSheetNote);
        var picked = trashSheet.Draw(screen, ActionSheetStyle.From(ui), trashSheetItems, Loc.T(L.Common.Cancel),
            false, title);
        if (picked < 0 || trashSheetNote is not { } note)
        {
            return;
        }

        trashSheetNote = null;
        if (picked == 0)
        {
            NoteTrash.Restore(configuration.Notes, configuration.RecentlyDeletedNotes, note);
            UiFeedback.Play(UiSound.Success);
            configuration.Save();
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Notes.DeleteNowConfirm),
            ConfirmLabel = Loc.T(L.Notes.Delete),
            CancelLabel = Loc.T(L.Notes.KeepIt),
            Sheet = true,
            Confirm = () => DeleteForGood(note),
        });
    }

    private void DeleteForGood(PhoneNote note)
    {
        configuration.RecentlyDeletedNotes.Remove(note);
        rowTexts.Remove(note.Id);
        configuration.Save();
    }

    private void AskDeleteAll()
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Notes.DeleteAllConfirm),
            ConfirmLabel = Loc.T(L.Notes.DeleteAll),
            CancelLabel = Loc.T(L.Notes.KeepIt),
            Sheet = true,
            Confirm = DeleteAllForGood,
        });
    }

    private void DeleteAllForGood()
    {
        var trash = configuration.RecentlyDeletedNotes;
        for (var index = 0; index < trash.Count; index++)
        {
            rowTexts.Remove(trash[index].Id);
        }

        trash.Clear();
        configuration.Save();
    }

    private void CloseTrash()
    {
        trashSheet.Close();
        router.Pop();
    }
}
