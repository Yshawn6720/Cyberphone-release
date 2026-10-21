using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private const float EditorTopBarHeight = 52f;
    private const float EditorNoticeOffset = 58f;
    private const float EditorResetLift = 18f;
    private const float EditorTopScrim = 110f;
    private const float EditorSavingRadius = 13f;


    private readonly PhotoEditSession editSession = new();

    private void OpenEditor(string path)
    {
        editSession.Open(path);
        router.Push(PhotoView.Editor());
    }

    private void CloseEditor()
    {
        editSession.Close();
        router.Pop();
    }

    private void DrawEditor(Rect screen)
    {
        var scale = UiScale.Current;
        if (!editSession.IsOpen)
        {
            if (router.Current.Route == PhotoRoute.Editor)
            {
                router.Pop(false);
            }

            return;
        }

        var safe = ContentWithin(screen);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(ViewerBackdrop));
        var panelTop = safe.Max.Y - PhotoEditPanel.Height * scale;
        var panel = new Rect(new Vector2(screen.Min.X, panelTop), screen.Max);
        var stage = new Rect(new Vector2(screen.Min.X, safe.Min.Y + EditorTopBarHeight * scale),
            new Vector2(screen.Max.X, panelTop));
        var style = PhotoEditPanelStyle.Dark(ui.Accent);
        PhotoEditPanel.DrawStage(editSession, stage, style, scale, ImGui.GetTime());
        PhotoEditPanel.DrawTools(editSession, panel, safe.Max.Y, ui, style, scale);
        DrawEditorTopBar(drawList, screen, safe, scale);
        if (editSession.IsDirty && !editSession.Saving)
        {
            var resetCenter = new Vector2(screen.Center.X, panelTop - EditorResetLift * scale);
            var resetLabel = Loc.T(L.Photos.Reset);
            var resetHalf = new Vector2(Button.WidthFor(resetLabel, ButtonSize.Small),
                Button.SmallHeight * scale) * 0.5f;
            if (Button.Draw(drawList, new Rect(resetCenter - resetHalf, resetCenter + resetHalf), resetLabel,
                    EditorInk(), ButtonStyle.Gray))
            {
                editSession.Reset();
                UiFeedback.Play(UiSound.Refresh);
            }
        }

        if (!editSession.Saving)
        {
            return;
        }

        Material.Veil(drawList, stage.Min, stage.Max, 0.45f, 0f);
        LoadingPulse.Draw(stage.Center, EditorSavingRadius * scale, ui.Accent, WhiteMuted, Loc.T(L.Account.Saving));
    }

    private ControlInk EditorInk() => new(ui.Accent, White, WhiteMuted, frameTheme.Danger);

    private void DrawEditorTopBar(ImDrawListPtr drawList, Rect screen, Rect safe, float scale)
    {
        PhotosChrome.TopScrim(drawList, screen.Min, screen.Max, EditorTopScrim * scale, 1f);
        var rowCenterY = safe.Min.Y + EditorTopBarHeight * scale * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, rowCenterY), Loc.T(L.Photos.Edit), White,
            TextStyles.Headline);
        var radius = ViewerControlRadius * scale;
        var cancelCenter = new Vector2(screen.Min.X + ViewerEdgeInset * scale + radius, rowCenterY);
        if (ViewerControl(drawList, "photos.editor.cancel", cancelCenter, PhoneIcons.X, Loc.T(L.Common.Cancel), White,
                1f, !editSession.Saving, HoverLabelSide.Below, scale))
        {
            CloseEditor();
            return;
        }

        var canSave = editSession.Preview.Ready && editSession.IsDirty && !editSession.Saving;
        var saveLabel = Loc.T(L.Photos.Save);
        var saveWidth = Button.WidthFor(saveLabel, ButtonSize.Regular);
        var saveHalfHeight = Button.RegularHeight * scale * 0.5f;
        var right = screen.Max.X - ViewerEdgeInset * scale;
        var save = new Rect(new Vector2(right - saveWidth, rowCenterY - saveHalfHeight),
            new Vector2(right, rowCenterY + saveHalfHeight));
        if (Button.Draw(drawList, save, saveLabel, EditorInk(), ButtonStyle.Prominent, enabled: canSave,
                id: "photos.editor.save"))
        {
            SaveEdit();
        }

        if (editSession.Notice.Length > 0)
        {
            Typography.DrawCentered(drawList, new Vector2(screen.Center.X, safe.Min.Y + EditorNoticeOffset * scale),
                editSession.Notice, frameTheme.Danger, TextStyles.Footnote);
        }
    }

    private void SaveEdit()
    {
        editSession.Saving = true;
        editSession.Notice = string.Empty;
        _ = SaveEditAsync(editSession.Snapshot());
    }

    private async Task SaveEditAsync(PhotoSaveRequest request)
    {
        string? saved = null;
        try
        {
            var rendered = await Task.Run(() => PhotoEditSession.Render(request)).ConfigureAwait(false);
            saved = await Task.Run(() => library.SaveEdited(rendered.Pixels, rendered.Width, rendered.Height))
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] saving the edit of {Path.GetFileName(request.Path)} failed");
        }

        await Plugin.Framework.RunOnFrameworkThread(() => FinishSave(request.Path, saved)).ConfigureAwait(false);
    }

    private void FinishSave(string source, string? saved)
    {
        editSession.Saving = false;
        if (router.Current.Route != PhotoRoute.Editor)
        {
            if (saved is not null && PhotoPlaces.CopyStamp(configuration.PhotoPlaces, source, saved))
            {
                configuration.Save();
            }

            return;
        }

        if (saved is null)
        {
            UiFeedback.Play(UiSound.Caution);
            editSession.Notice = Loc.T(L.Photos.EditFailed);
            return;
        }

        if (PhotoPlaces.CopyStamp(configuration.PhotoPlaces, source, saved))
        {
            configuration.Save();
        }

        UiFeedback.Play(UiSound.Success);
        Refresh();
        var insertAt = Math.Clamp(viewerIndex, 0, viewerPaths.Length);
        var expanded = new string[viewerPaths.Length + 1];
        for (var index = 0; index < insertAt; index++)
        {
            expanded[index] = viewerPaths[index];
        }

        expanded[insertAt] = saved;
        for (var index = insertAt; index < viewerPaths.Length; index++)
        {
            expanded[index + 1] = viewerPaths[index];
        }

        viewerPaths = expanded;
        viewerIndex = insertAt;
        viewerTitlePath = string.Empty;
        ResetViewerMotion();
        CloseEditor();
    }
}
