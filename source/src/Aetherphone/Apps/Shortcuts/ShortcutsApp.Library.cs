using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const int SearchMaxLength = 40;
    private const float SearchGap = 14f;
    private const float HoverLift = 0.04f;

    private readonly NavBarButton[] libraryButtons = new NavBarButton[2];
    private readonly List<ShortcutEntry> matchBuffer = new();
    private readonly DropdownMenu menu = new();
    private readonly List<DropdownMenu.Item> menuItems = new();
    private Guid menuTargetId = Guid.Empty;
    private bool menuTargetPinned;
    private string libraryQuery = string.Empty;
    private CachedText runStatus;

    private void DrawLibrary(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var scale = UiScale.Current;
        using (AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = store.All.Count == 0
                ? DrawEmptyLibrary(drawList, origin, width, scale)
                : DrawLibraryContent(drawList, origin, width, scale);
            ShortcutsArt.ReserveTo(origin, width, cursorY + ShortcutsArt.BottomPad * scale);
        }

        libraryButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.FileImport), Loc.T(L.Shortcuts.ImportShortcut));
        libraryButtons[1] = new NavBarButton(PhoneIcons.Plus, Loc.T(L.Shortcuts.NewShortcut));
        UiAnchors.Report("shortcuts.import", AppHeader.LargeTitleButtonRect(in navBar, 0, libraryButtons.Length));
        UiAnchors.Report("shortcuts.new", AppHeader.LargeTitleButtonRect(in navBar, 1, libraryButtons.Length));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "shortcuts.nav.library", DisplayName,
            NavBarStyle.From(ui), libraryButtons);
        if (pressed == 0)
        {
            BeginImport();
        }
        else if (pressed == 1)
        {
            StartNewShortcut();
        }
    }

    private float DrawEmptyLibrary(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var bottom = ShortcutsArt.State(drawList, ui, origin, width, FontAwesomeIcon.Bolt,
            Loc.T(L.Shortcuts.LibraryEmpty), Loc.T(L.Shortcuts.LibraryEmptyBody), Loc.T(L.Shortcuts.NewShortcut),
            Loc.T(L.Shortcuts.BrowseGallery), out var create, out var browse, scale);
        if (create)
        {
            StartNewShortcut();
        }
        else if (browse)
        {
            activeTab = ShortcutsTab.Gallery;
        }

        return bottom;
    }

    private float DrawLibraryContent(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
        GlassField.Search(drawList, field, "##shortcutsSearch", Loc.T(L.Shortcuts.SearchShortcuts), ref libraryQuery,
            theme, scale, SearchMaxLength, false);
        var top = field.Max.Y + SearchGap * scale;

        matchBuffer.Clear();
        var shortcuts = store.All;
        for (var index = 0; index < shortcuts.Count; index++)
        {
            if (Matches(shortcuts[index], libraryQuery))
            {
                matchBuffer.Add(shortcuts[index]);
            }
        }

        if (matchBuffer.Count == 0)
        {
            return ShortcutsArt.State(drawList, ui, new Vector2(origin.X, top), width, FontAwesomeIcon.Search,
                Loc.T(L.Shortcuts.NoResults), Loc.T(L.Shortcuts.NoMatches), string.Empty, string.Empty, out _, out _,
                scale);
        }

        var run = runner.Snapshot();
        var bottom = top;
        for (var index = 0; index < matchBuffer.Count; index++)
        {
            var rect = TileRect(new Vector2(origin.X, top), width, index, scale);
            bottom = rect.Max.Y;
            if (index == 0)
            {
                UiAnchors.Report("shortcuts.tile", rect);
            }

            if (!ImGui.IsRectVisible(rect.Min, rect.Max))
            {
                continue;
            }

            DrawLibraryTile(drawList, rect, index, matchBuffer[index], run, scale);
        }

        return bottom;
    }

    private static Rect TileRect(Vector2 origin, float width, int index, float scale)
    {
        var gap = ShortcutsArt.TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = ShortcutsArt.TileHeight * scale;
        var column = index % 2;
        var row = index / 2;
        var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
        return new Rect(min, min + new Vector2(tileWidth, tileHeight));
    }

    private static Rect Pressed(Rect rect, uint key, bool down)
    {
        var grow = PressFx.Scale(key, down, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * grow;
        return new Rect(rect.Center - half, rect.Center + half);
    }

    private void DrawLibraryTile(ImDrawListPtr drawList, Rect rect, int index, ShortcutEntry entry,
        in ShortcutRunView run, float scale)
    {
        var running = run.IsRunning && run.Id == entry.Id;
        var corner = ShortcutsArt.CornerCenter(rect, scale);
        var cornerHit = new Vector2(ShortcutsArt.CornerHit * scale);
        var overCorner = !running && UiInteract.Hover(corner - cornerHit, corner + cornerHit);
        var hovered = !overCorner && UiInteract.Hover(rect.Min, rect.Max);
        var drawn = Pressed(rect, ImGui.GetID($"##shortcutTile{index}"),
            hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        ShortcutsArt.TileBody(drawList, drawn, ShortcutTint.Resolve(entry.Tint), hovered ? HoverLift : 0f, scale);
        ShortcutsArt.TileGlyph(drawList, drawn, entry.Glyph, entry.Name, store.Icon(entry), scale);
        ShortcutsArt.TileName(drawList, drawn, ShortcutRunText.Name(entry.Name), running ? RunStatus(run) : string.Empty,
            scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var drawnCorner = ShortcutsArt.CornerCenter(drawn, scale);
        if (running)
        {
            ShortcutsArt.RunRing(drawList, drawnCorner, ShortcutsArt.CornerRadius * scale, run.Progress,
                new Vector4(1f, 1f, 1f, 1f), scale);
        }
        else if (ShortcutsArt.CornerButton(drawList, drawnCorner, FontAwesomeIcon.EllipsisH, Loc.T(L.Shortcuts.Edit),
                     scale, out _))
        {
            StartEditShortcut(entry);
            return;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenTileMenu(entry, rect);
            return;
        }

        if (overCorner || !UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return;
        }

        if (running)
        {
            runner.Cancel();
            return;
        }

        RunEntry(entry);
    }

    private void RunEntry(ShortcutEntry entry)
    {
        if (!ShortcutRules.HasRunnableStep(entry))
        {
            UiFeedback.Play(UiSound.Blocked);
            StartEditShortcut(entry);
            return;
        }

        runner.Run(entry);
    }

    private string RunStatus(in ShortcutRunView run)
    {
        var key = ((long)run.StepCount << 32) | ((long)run.Step << 1) | (run.Holding ? 1L : 0L);
        return runStatus.IsCurrent(key) ? runStatus.Value : runStatus.Store(key, ShortcutRunText.Status(run));
    }

    private static bool Matches(ShortcutEntry entry, string query)
    {
        var trimmed = query.AsSpan().Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (entry.Name.AsSpan().Contains(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 0; index < entry.Steps.Count; index++)
        {
            if (entry.Steps[index].Text.AsSpan().Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void OpenTileMenu(ShortcutEntry entry, Rect anchor)
    {
        UiFeedback.Play(UiSound.Tap);
        menuTargetId = entry.Id;
        menuTargetPinned = store.IsPinned(entry.Id);
        menuItems.Clear();
        menuItems.Add(new DropdownMenu.Item(Loc.T(L.Shortcuts.TestRun), IconGlyph.Of(FontAwesomeIcon.Play)));
        menuItems.Add(new DropdownMenu.Item(Loc.T(L.Shortcuts.Edit), PhoneIcons.Pencil));
        menuItems.Add(new DropdownMenu.Item(Loc.T(L.Shortcuts.Duplicate), PhoneIcons.Copy));
        menuItems.Add(new DropdownMenu.Item(Loc.T(L.Shortcuts.Share), PhoneIcons.Share));
        menuItems.Add(new DropdownMenu.Item(Loc.T(menuTargetPinned ? L.Shortcuts.RemoveFromHome : L.Shortcuts.AddToHome),
            PhoneIcons.Home));
        menuItems.Add(new DropdownMenu.Item(Loc.T(L.Shortcuts.Delete), PhoneIcons.Trash, true));
        menu.Header = ShortcutRunText.Name(entry.Name);
        menu.Toggle("shortcuts.tileMenu", anchor);
    }

    private void DrawMenu(Rect screen)
    {
        if (!menu.Open || menuItems.Count == 0)
        {
            return;
        }

        var picked = menu.Draw(screen, theme, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(menuItems));
        if (picked < 0)
        {
            return;
        }

        menuItems.Clear();
        var entry = store.Find(menuTargetId);
        if (entry is null)
        {
            return;
        }

        switch (picked)
        {
            case 0:
                RunEntry(entry);
                break;
            case 1:
                StartEditShortcut(entry);
                break;
            case 2:
                DuplicateEntry(entry);
                break;
            case 3:
                CopyCode(entry);
                break;
            case 4:
                store.SetPinned(entry.Id, !menuTargetPinned);
                UiFeedback.Play(menuTargetPinned ? UiSound.ToggleOff : UiSound.ToggleOn);
                break;
            default:
                AskDelete(entry.Id, false);
                break;
        }
    }

    private ShortcutEntry? DuplicateEntry(ShortcutEntry source)
    {
        if (WarnIfFull())
        {
            return null;
        }

        var copy = source.Copy();
        copy.Name = CopyName(source.Name);
        ShortcutRules.PruneEmptySteps(copy);
        if (copy.IconImage.Length > 0)
        {
            copy.IconImage = store.DuplicateIcon(copy.IconImage) ?? string.Empty;
        }

        store.Add(copy);
        UiFeedback.Play(UiSound.Success);
        return copy;
    }

    private static void CopyCode(ShortcutEntry entry)
    {
        ImGui.SetClipboardText(ShortcutCode.Encode(entry));
        ShellToast.Show();
    }

    private static string CopyName(string name)
    {
        var candidate = Loc.T(L.Shortcuts.CopyName, ShortcutRunText.Name(name).Trim());
        return candidate.Length <= ShortcutStore.NameMaxLength
            ? candidate
            : candidate.Substring(0, ShortcutStore.NameMaxLength);
    }

    private void AskDelete(Guid id, bool fromEditor)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Shortcuts.DeleteConfirm),
            ConfirmLabel = Loc.T(L.Shortcuts.Delete),
            CancelLabel = Loc.T(L.Shortcuts.KeepIt),
            Sheet = true,
            Confirm = () => DeleteShortcut(id, fromEditor),
        });
    }

    private void DeleteShortcut(Guid id, bool fromEditor)
    {
        if (runner.IsRunning && runner.Snapshot().Id == id)
        {
            runner.Cancel();
        }

        store.Remove(id);
        if (!fromEditor)
        {
            return;
        }

        DiscardUnsavedIcon();
        draft = null;
        draftBaseline = null;
        draftId = Guid.Empty;
        router.Reset();
    }

    private void Warn(string message) => confirm.Alert(null, message, Loc.T(L.Shortcuts.Ok));

    private bool WarnIfFull()
    {
        if (!store.AtCapacity)
        {
            return false;
        }

        UiFeedback.Play(UiSound.Blocked);
        Warn(Loc.T(L.Shortcuts.LimitReached, ShortcutStore.MaxShortcuts));
        return true;
    }
}
