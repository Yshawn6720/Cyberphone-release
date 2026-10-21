using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private volatile bool iconSaving;
    private volatile bool iconFailed;
    private byte[]? pendingIconBytes;
    private ShortcutEntry? iconBakeDraft;

    private void OpenCustomIconPicker()
    {
        iconFailed = false;
        iconPicker.Open();
        Push(ShortcutsRoute.CustomIcon, Loc.T(L.Shortcuts.Appearance));
    }

    private void DrawCustomIconPicker(Rect content)
    {
        if (draft is null)
        {
            return;
        }

        if (iconFailed)
        {
            iconFailed = false;
            Warn(Loc.T(L.Shortcuts.CustomIconFailed));
        }

        var context = new PhoneContext(content, theme, navigation);
        var labels = new ImagePickCropLabels(Loc.T(L.Shortcuts.CustomIconTitle), Loc.T(L.Common.ImportFromPc),
            Loc.T(L.Common.NoPhotos), Loc.T(L.Shortcuts.CustomIconMoveAndScale), Loc.T(L.Shortcuts.CustomIconUse),
            Loc.T(L.Shortcuts.CustomIconSaving), Loc.T(L.Shortcuts.CustomIconGestureHint));
        var result = iconPicker.Draw(content, context, labels, ui.Accent, iconSaving);
        if (result == ImagePickCropEvent.Cancelled)
        {
            router.Pop();
            return;
        }

        if (result == ImagePickCropEvent.Committed && !iconSaving && iconPicker.SourcePath.Length > 0)
        {
            BakeCustomIcon(iconPicker.SourcePath, iconPicker.Crop);
        }
    }

    private void BakeCustomIcon(string sourcePath, WallpaperCrop crop)
    {
        iconSaving = true;
        iconBakeDraft = draft;
        _ = Task.Run(() =>
        {
            try
            {
                var bytes = ShortcutIconLibrary.Bake(sourcePath, crop);
                Interlocked.Exchange(ref pendingIconBytes, bytes);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Shortcuts] failed to bake a custom icon");
                iconSaving = false;
                iconFailed = true;
                iconBakeDraft = null;
            }
        });
    }

    private void ConsumeBakedIcon()
    {
        var bytes = Interlocked.Exchange(ref pendingIconBytes, null);
        if (bytes is null)
        {
            return;
        }

        iconSaving = false;
        var target = iconBakeDraft;
        iconBakeDraft = null;
        if (target is null || !ReferenceEquals(target, draft))
        {
            return;
        }

        var previousUnsaved = UnsavedIconOf(target);
        store.SetCustomIcon(target, bytes);
        if (previousUnsaved.Length > 0)
        {
            store.ReleaseIcon(previousUnsaved);
        }

        if (router.Current.Route == ShortcutsRoute.CustomIcon)
        {
            router.Pop();
        }
    }

    private void DiscardUnsavedIcon()
    {
        if (draft is null)
        {
            return;
        }

        var unsaved = UnsavedIconOf(draft);
        if (unsaved.Length == 0)
        {
            return;
        }

        store.ReleaseIcon(unsaved);
        draft.IconImage = string.Empty;
    }

    private string UnsavedIconOf(ShortcutEntry entry)
    {
        if (entry.IconImage.Length == 0)
        {
            return string.Empty;
        }

        var persisted = draftId == Guid.Empty ? null : store.Find(draftId);
        return persisted is not null && persisted.IconImage == entry.IconImage ? string.Empty : entry.IconImage;
    }
}
