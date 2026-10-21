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

internal enum NotesScreen : byte
{
    List,
    EditNote,
    EditReminder,
    RecentlyDeleted,
}

internal enum NotesTab : byte
{
    Notes,
    Reminders,
}

internal enum NoteAction : byte
{
    TogglePin,
    Copy,
    Delete,
}

internal sealed partial class NotesApp : IResumableApp, ISpotlightNotes, ITabRouteTarget
{
    private const string RemindersTabKey = "notes.tab.reminders";

    public string Id => "notes";
    public string DisplayName => Loc.T(L.Apps.Notes);
    public string Glyph => "N";
    public Vector4 Accent => AppAccents.For("notes");
    public int BadgeCount => 0;
    public bool WantsSystemTheme => true;

    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Notes(PhoneTheme.Default));
    private readonly ViewRouter<NotesScreen> router;
    private readonly RouterDraw<NotesScreen> drawView;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[2];
    private readonly ActionSheet noteSheet = new();
    private readonly ActionSheet.Item[] noteSheetItems = new ActionSheet.Item[3];
    private readonly NoteAction[] noteSheetActions = new NoteAction[3];
    private int noteSheetCount;
    private PendingTab pendingTab;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private NotesTab activeTab;
    private PhoneNote? sheetNote;
    private bool sheetFromEditor;

    public NotesApp(Configuration configuration, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.confirm = confirm;
        router = new ViewRouter<NotesScreen>(NotesScreen.List);
        drawView = DrawView;
        closeNoteEditor = CloseNoteEditor;
        closeReminderEditor = CloseReminderEditor;
        closeTrash = CloseTrash;
    }

    public void OpenTab(string tab) => pendingTab.Request(tab);

    public void RequestNote(Guid noteId) => pendingNoteId = noteId;

    public void RequestNewNote() => pendingNewNote = true;

    public void OnOpened()
    {
        router.Reset();
        editingNote = null;
        activeTab = NotesTab.Notes;
        searchQuery = string.Empty;
        showCompleted = false;
        reminderFilter = ReminderFilter.All;
        if (NoteTrash.Purge(configuration.RecentlyDeletedNotes, DateTime.Now))
        {
            configuration.Save();
        }

        ConsumePendingNote();
    }

    public void OnResumed()
    {
        ReopenClosedEditor();
        ConsumePendingNote();
    }

    private void ReopenClosedEditor()
    {
        if (router.Current != NotesScreen.EditNote || editingNote is not null)
        {
            return;
        }

        if (shownNote is { } note && configuration.Notes.Contains(note))
        {
            OpenEditor(note);
            return;
        }

        router.Reset();
    }

    public void OnClosed()
    {
        CommitOpenEditors();
        noteSheet.Close();
        trashSheet.Close();
    }

    private void CommitOpenEditors()
    {
        CommitNoteBuffer();
        if (router.Current == NotesScreen.EditReminder)
        {
            CommitReminder();
        }
    }

    private void ConsumePendingNote()
    {
        if (pendingNewNote)
        {
            pendingNewNote = false;
            ReturnToList();
            StartNewNote();
            return;
        }

        if (pendingNoteId is not { } id)
        {
            return;
        }

        pendingNoteId = null;
        var notes = configuration.Notes;
        for (var index = 0; index < notes.Count; index++)
        {
            if (notes[index].Id != id)
            {
                continue;
            }

            ReturnToList();
            StartEditNote(notes[index]);
            return;
        }
    }

    private void ReturnToList()
    {
        CommitOpenEditors();
        router.Reset();
        activeTab = NotesTab.Notes;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        ui.Palette = AppPalettes.Notes(context.Theme);
        if (pendingTab.Take(RemindersTabKey))
        {
            ReturnToList();
            activeTab = NotesTab.Reminders;
        }

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        noteSheet.Gate();
        trashSheet.Gate();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawNoteSheet(screen);
        DrawTrashSheet(screen);
        AutosaveNote();
    }

    private void DrawView(NotesScreen screen, Rect area, int depth)
    {
        ui.Body(area);
        var live = depth == router.Depth;
        switch (screen)
        {
            case NotesScreen.EditNote:
                DrawNoteEditor(area, live);
                return;
            case NotesScreen.EditReminder:
                DrawReminderEditor(area);
                return;
            case NotesScreen.RecentlyDeleted:
                DrawTrash(area);
                return;
            default:
                DrawRoot(area);
                return;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        using (TabBar.ReserveContent(scale))
        {
            if (activeTab == NotesTab.Reminders)
            {
                DrawReminders(context);
            }
            else
            {
                DrawNotes(context);
            }
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[0] = new TabItem(Loc.T(L.Notes.TabNotes), PhoneIcons.FileText);
        tabItems[1] = new TabItem(Loc.T(L.Notes.TabReminders), PhoneIcons.CircleCheck, PhoneIcons.CircleCheckFilled,
            AnchorKey: RemindersTabKey);
        var actionLabel = activeTab == NotesTab.Notes ? Loc.T(L.Notes.NewNote) : Loc.T(L.Notes.NewReminder);
        var action = new TabBarAction(PhoneIcons.Plus, actionLabel, AnchorKey: "notes.new");
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab, action);
        if (result.ActionTapped)
        {
            if (activeTab == NotesTab.Notes)
            {
                StartNewNote();
            }
            else
            {
                StartNewReminder();
            }

            return;
        }

        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        activeTab = (NotesTab)result.Tapped;
    }

    private void OpenNoteSheet(PhoneNote note, bool fromEditor)
    {
        sheetNote = note;
        sheetFromEditor = fromEditor;
        noteSheetCount = 0;
        AddSheetItem(NoteAction.TogglePin, new ActionSheet.Item(Loc.T(note.Pinned ? L.Common.Unpin : L.Common.Pin),
            note.Pinned ? PhoneIcons.PinFilled : PhoneIcons.Pin));
        if (!fromEditor)
        {
            AddSheetItem(NoteAction.Copy, new ActionSheet.Item(Loc.T(L.Notes.CopyNote), PhoneIcons.Copy));
        }

        AddSheetItem(NoteAction.Delete, new ActionSheet.Item(Loc.T(L.Notes.DeleteNote), PhoneIcons.Trash, true));
        noteSheet.Open();
    }

    private void AddSheetItem(NoteAction action, in ActionSheet.Item item)
    {
        noteSheetActions[noteSheetCount] = action;
        noteSheetItems[noteSheetCount] = item;
        noteSheetCount++;
    }

    private void DrawNoteSheet(Rect screen)
    {
        if (!noteSheet.CapturesPointer)
        {
            return;
        }

        var expected = sheetFromEditor ? NotesScreen.EditNote : NotesScreen.List;
        if (noteSheet.IsOpen && router.Current != expected)
        {
            noteSheet.Close();
        }

        var title = sheetNote is null ? string.Empty : NoteTitle(sheetNote);
        var picked = noteSheet.Draw(screen, ActionSheetStyle.From(ui), noteSheetItems.AsSpan(0, noteSheetCount),
            Loc.T(L.Common.Cancel), false, title);
        if (picked < 0 || picked >= noteSheetCount || sheetNote is not { } note)
        {
            return;
        }

        sheetNote = null;
        switch (noteSheetActions[picked])
        {
            case NoteAction.TogglePin:
                TogglePinned(note);
                break;
            case NoteAction.Copy:
                CopyNote(note);
                break;
            default:
                TrashNote(note, sheetFromEditor);
                break;
        }
    }

    private string NoteTitle(PhoneNote note)
    {
        var text = RowTextFor(note);
        return text.Title.Length > 0 ? text.Title : Loc.T(L.Notes.Untitled);
    }

    private void TogglePinned(PhoneNote note)
    {
        note.Pinned = !note.Pinned;
        UiFeedback.Play(note.Pinned ? UiSound.ToggleOn : UiSound.ToggleOff);
        configuration.Save();
    }

    private void CopyNote(PhoneNote note)
    {
        var body = ReferenceEquals(note, editingNote) ? noteEditor.Text : note.Body;
        ImGui.SetClipboardText(body);
        ShellToast.Show();
    }

    private void TrashNote(PhoneNote note, bool leaveEditor)
    {
        if (ReferenceEquals(note, editingNote))
        {
            note.Body = noteEditor.Text;
            editingNote = null;
            noteDirty = false;
        }

        NoteTrash.Discard(configuration.Notes, configuration.RecentlyDeletedNotes, note, DateTime.Now);
        configuration.Save();
        ShellToast.Show(Loc.T(L.Notes.MovedToTrash));
        if (leaveEditor && router.Current == NotesScreen.EditNote)
        {
            router.Pop();
        }
    }

    public void Dispose()
    {
    }
}
