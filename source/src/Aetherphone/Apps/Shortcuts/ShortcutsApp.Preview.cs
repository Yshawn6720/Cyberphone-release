using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const float HeroHeight = 132f;
    private const float AddButtonHeight = Button.LargeHeight;
    private const float NoteGap = 10f;

    private ShortcutEntry? previewEntry;
    private CachedText previewSteps;

    private void BeginImport()
    {
        if (WarnIfFull())
        {
            return;
        }

        if (!ShortcutCode.TryDecode(ImGui.GetClipboardText(), out var decoded, out var error))
        {
            UiFeedback.Play(UiSound.Blocked);
            Warn(ImportErrorText(error));
            return;
        }

        previewEntry = decoded;
        Push(ShortcutsRoute.Preview, RootTitle());
    }

    private static string ImportErrorText(ShortcutCodeError error) => error switch
    {
        ShortcutCodeError.UnsafeLink => Loc.T(L.Shortcuts.ImportUnsafeLink),
        ShortcutCodeError.Malformed => Loc.T(L.Shortcuts.ImportMalformed),
        _ => Loc.T(L.Shortcuts.ImportBadCode),
    };

    private void DrawPreview(in PhoneContext context, ShortcutsView view)
    {
        var entry = previewEntry;
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        if (entry is not null)
        {
            using (AppSurface.Begin(navBar.Body))
            {
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = DrawPreviewHero(drawList, origin, width, entry, scale);
                cursorY += ShortcutsArt.SectionGap * scale * 0.5f;
                cursorY += CardSectionHeader.Draw(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Shortcuts.ImportWillRun), ui.TitleInk) + ShortcutsArt.HeaderGap * scale;
                cursorY = DrawReadOnlySteps(drawList, new Vector2(origin.X, cursorY), width, entry.Steps, scale);
                cursorY = DrawPreviewAction(drawList, new Vector2(origin.X, cursorY), width, entry, scale);
                ShortcutsArt.ReserveTo(origin, width, cursorY + ShortcutsArt.BottomPad * scale);
            }
        }

        var title = entry is null ? Loc.T(L.Shortcuts.ImportShortcut) : ShortcutRunText.Name(entry.Name);
        AppHeader.EndLargeTitle(in navBar, context, "shortcuts.nav.preview", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private float DrawPreviewHero(ImDrawListPtr drawList, Vector2 origin, float width, ShortcutEntry entry,
        float scale)
    {
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + HeroHeight * scale));
        ShortcutsArt.TileBody(drawList, rect, ShortcutTint.Resolve(entry.Tint), 0f, scale);
        ShortcutsArt.TileGlyph(drawList, rect, entry.Glyph, entry.Name, store.Icon(entry), scale);
        var count = entry.Steps.Count;
        var steps = previewSteps.IsCurrent(count)
            ? previewSteps.Value
            : previewSteps.Store(count, Loc.Plural(L.Shortcuts.StepCount, count));
        ShortcutsArt.TileName(drawList, rect, ShortcutRunText.Name(entry.Name), steps, scale);
        return rect.Max.Y;
    }

    private float DrawPreviewAction(ImDrawListPtr drawList, Vector2 origin, float width, ShortcutEntry entry,
        float scale)
    {
        var top = origin.Y + ShortcutsArt.SectionGap * scale;
        if (store.FindByName(entry.Name) is not null)
        {
            top += Typography.DrawWrappedLeft(new Vector2(origin.X, top), Loc.T(L.Shortcuts.AlreadyInLibrary),
                ui.MutedInk, TextStyles.Footnote, width) + NoteGap * scale;
        }

        var rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + AddButtonHeight * scale));
        if (ui.AccentPill(rect, Loc.T(L.Shortcuts.ImportAdd), true, TextStyles.Headline))
        {
            CommitPreview();
        }

        return rect.Max.Y;
    }

    private void CommitPreview()
    {
        if (previewEntry is null || router.Current.Route != ShortcutsRoute.Preview || WarnIfFull())
        {
            return;
        }

        store.Add(previewEntry);
        UiFeedback.Play(UiSound.Success);
        ShellToast.Show(Loc.T(L.Shortcuts.AddedToLibrary));
        activeTab = ShortcutsTab.Library;
        libraryQuery = string.Empty;
        router.Pop();
    }
}
