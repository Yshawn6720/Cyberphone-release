using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float AddSheetHeight = 0.5f;
    private const float AddSheetViewerHeight = 0.34f;
    private const float AddSheetSwitcherHeight = 0.42f;
    private const float FieldRowHeight = 44f;
    private const float ClipboardPollSeconds = 1f;
    private const int MaxLinkLength = 2000;
    private const int PlaylistActionOne = 0;
    private const int PlaylistActionAll = 1;
    private const int AddTargetScreenB = 3;
    private const int SwitchTargetScreenA = 0;
    private const int SwitchTargetScreenB = 1;
    private const int SwitchTargetQueue = 2;

    private readonly SheetSurface addSheet = new("aetherstream.add");
    private readonly string[] addModeLabels = new string[4];
    private readonly string[] switchModeLabels = new string[3];
    private readonly Action<Rect> drawAddContent;

    private string linkInput = string.Empty;
    private string focusLinkFieldId = string.Empty;
    private string copiedLink = string.Empty;
    private string copiedLinkHost = string.Empty;
    private string dismissedCopiedLink = string.Empty;
    private QueueAddMode addMode = QueueAddMode.PlayNow;
    private bool addTargetsScreenB;
    private QueueAddMode pendingPlaylistMode = QueueAddMode.PlayNow;
    private RefreshCadence clipboardCadence;
    private uint clipboardSequence = ClipboardWatch.UnseenSequence;

    private void OpenAddSheet()
    {
        addMode = video.HasMedia || queue.Current is not null ? QueueAddMode.AddToQueue : QueueAddMode.PlayNow;
        addTargetsScreenB = false;
        tracksSheet.Close();
        addSheet.Open();
    }

    private void OpenAddSheetForScreenB()
    {
        OpenAddSheet();
        addTargetsScreenB = true;
    }

    private void DrawAddSheet(Rect area, float scale)
    {
        var viewing = watchAlong.IsViewing;
        var switching = viewing && watchAlong.CanSwitchVideo;
        var title = viewing && !switching
            ? Loc.T(watchAlong.CanAddDirectly ? L.AetherStream.AddToPartyTitle : L.AetherStream.SuggestTitle)
            : Loc.T(L.AetherStream.AddVideoTitle);
        var height = !viewing ? AddSheetHeight : switching ? AddSheetSwitcherHeight : AddSheetViewerHeight;
        addSheet.Draw(area, SheetSkin.From(ui.Palette, Ink), title, height, drawAddContent);
    }

    private void DrawAddContent(Rect content)
    {
        var scale = UiScale.Current;
        var viewing = watchAlong.IsViewing;
        var switching = viewing && watchAlong.CanSwitchVideo;
        var top = content.Min.Y;
        var submitted = DrawLinkField(new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, top + FieldRowHeight * scale)), "##aetherstreamAddLink",
            "aetherstream.add.paste", scale);
        top += FieldRowHeight * scale + Metrics.Space.Md * scale;

        if (switching)
        {
            switchModeLabels[SwitchTargetScreenA] = Loc.T(L.AetherStream.PlayNow);
            switchModeLabels[SwitchTargetScreenB] = Loc.T(L.AetherStream.ScreenB);
            switchModeLabels[SwitchTargetQueue] = Loc.T(watchAlong.CanAddDirectly
                ? L.AetherStream.AddToQueue
                : L.AetherStream.SuggestAction);
            var strip = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + 36f * scale));
            var current = addTargetsScreenB ? SwitchTargetScreenB
                : addMode == QueueAddMode.PlayNow ? SwitchTargetScreenA
                : SwitchTargetQueue;
            var picked = SegmentStrip.Draw("aetherstream.switchMode", strip, switchModeLabels, current, ui.Palette);
            addTargetsScreenB = picked == SwitchTargetScreenB;
            addMode = picked == SwitchTargetScreenA ? QueueAddMode.PlayNow : QueueAddMode.AddToQueue;
            top += 36f * scale + Metrics.Space.Md * scale;
        }

        if (!viewing)
        {
            addModeLabels[0] = Loc.T(L.AetherStream.PlayNow);
            addModeLabels[1] = Loc.T(L.AetherStream.PlayNext);
            addModeLabels[2] = Loc.T(L.AetherStream.AddToQueue);
            addModeLabels[3] = Loc.T(L.AetherStream.ScreenB);
            var strip = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + 36f * scale));
            var picked = SegmentStrip.Draw("aetherstream.addMode", strip, addModeLabels,
                addTargetsScreenB ? AddTargetScreenB : (int)addMode, ui.Palette);
            addTargetsScreenB = picked == AddTargetScreenB;
            if (!addTargetsScreenB)
            {
                addMode = (QueueAddMode)picked;
            }

            top += 36f * scale + Metrics.Space.Md * scale;
        }

        var canSubmit = MediaInput.Normalize(linkInput).Length > 0;
        var primary = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + ButtonHeight * scale));
        var switchesVideo = (!viewing || switching) && (addTargetsScreenB || addMode == QueueAddMode.PlayNow);
        var primaryLabel = viewing && !switchesVideo
            ? Loc.T(watchAlong.CanAddDirectly ? L.AetherStream.AddToQueue : L.AetherStream.SuggestAction)
            : Loc.T(switchesVideo ? L.AetherStream.PlayAction : L.AetherStream.QueueSuggestionAdd);
        if (PrimaryButton(primary, primaryLabel, canSubmit) || (submitted && canSubmit))
        {
            if ((!viewing || switching) && addTargetsScreenB)
            {
                SubmitToScreenB(linkInput);
                return;
            }

            SubmitInput(linkInput, addMode);
            return;
        }

        if (viewing)
        {
            DrawSupportedSitesHint(content, top + ButtonHeight * scale + Metrics.Space.Md * scale);
            return;
        }

        top += ButtonHeight * scale + Metrics.Space.Sm * scale;
        var browse = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + ButtonHeight * scale));
        if (QuietButton(browse, Loc.T(L.AetherStream.BrowseLocalFile)))
        {
            PickLocalFile(addTargetsScreenB);
        }

        DrawSupportedSitesHint(content, top + ButtonHeight * scale + Metrics.Space.Md * scale);
    }

    private void DrawSupportedSitesHint(Rect content, float top)
    {
        Typography.DrawWrappedLeft(new Vector2(content.Min.X, top), Loc.T(L.AetherStream.SupportedSitesHint),
            Ink.MutedInk, TextStyles.Footnote, content.Width);
    }

    private bool DrawLinkField(Rect row, string imguiId, string pasteId, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pasteRadius = RoundButton.RegularRadius * scale;
        var field = new Rect(row.Min, new Vector2(row.Max.X - pasteRadius * 2f - Metrics.Space.Sm * scale, row.Max.Y));
        var hint = watchAlong.IsViewing ? Loc.T(L.AetherStream.SuggestHint) : Loc.T(L.AetherStream.UrlHint);
        if (string.Equals(focusLinkFieldId, imguiId, StringComparison.Ordinal))
        {
            focusLinkFieldId = string.Empty;
            ImGui.SetKeyboardFocusHere();
        }

        var submitted = SubmitField.Draw(field, imguiId, hint, ref linkInput, accentedTheme, MaxLinkLength,
            FontAwesomeIcon.Link);
        var pasteCenter = new Vector2(row.Max.X - pasteRadius, row.Center.Y);
        if (StreamRoundButton(drawList, pasteId, pasteCenter, pasteRadius, FontAwesomeIcon.Paste, ButtonStyle.Gray,
                Loc.T(L.AetherStream.PasteClipboard), HoverLabelSide.Above))
        {
            var clipboard = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboard))
            {
                linkInput = MediaInput.Normalize(clipboard);
                focusLinkFieldId = imguiId;
            }
        }

        return submitted;
    }

    private void PickLocalFile(bool screenB)
    {
        if (screenB)
        {
            FilePicker.PickVideo(Loc.T(L.AetherStream.BrowseLocalFile),
                path => Interlocked.Exchange(ref pendingScreenBFile, path));
            return;
        }

        FilePicker.PickVideo(Loc.T(L.AetherStream.BrowseLocalFile),
            path => Interlocked.Exchange(ref pendingLocalFile, path));
    }

    private void SubmitToScreenB(string raw)
    {
        if (watchAlong.IsViewing)
        {
            if (watchAlong.CanSwitchVideo)
            {
                SwitchPartyVideo(raw, VideoChannel.B);
                return;
            }

            SubmitInput(raw, QueueAddMode.AddToQueue);
            return;
        }

        var input = MediaInput.Normalize(raw);
        if (!AcceptedOnScreenB(input))
        {
            return;
        }

        PlayOnScreenB(queue.CreateDisplayEntry(input), 0d);
        linkInput = string.Empty;
        addSheet.Close();
    }

    private bool PlayOnScreenB(VideoQueueEntry entry, double startSeconds)
    {
        if (!AcceptedOnScreenB(entry.Url))
        {
            return false;
        }

        CancelPendingJoin();
        suite.ChannelB.Play(entry, startSeconds);
        ShowWatch(VideoChannel.B);
        return true;
    }

    private static bool AcceptedOnScreenB(string input)
    {
        switch (DualVideo.CheckScreenBLink(input))
        {
            case ScreenBLink.Empty:
                return false;
            case ScreenBLink.NotALink:
                ShellToast.Show(Loc.T(L.AetherStream.NotALink));
                return false;
            case ScreenBLink.Playlist:
                ShellToast.Show(Loc.T(L.AetherStream.ScreenBOneVideo));
                return false;
        }

        return true;
    }

    private void SwitchPartyVideo(string raw, VideoChannel channel)
    {
        var input = MediaInput.Normalize(raw);
        if (input.Length == 0)
        {
            return;
        }

        if (MediaInput.LooksLikeLocalPath(input))
        {
            ShellToast.Show(Loc.T(L.AetherStream.SuggestLinksOnly));
            return;
        }

        if (!VideoEngine.ValidateURL(input, out _))
        {
            ShellToast.Show(Loc.T(L.AetherStream.NotALink));
            return;
        }

        if (PartySwitch.Link(input) is not { } link)
        {
            ShellToast.Show(Loc.T(channel == VideoChannel.B
                ? L.AetherStream.ScreenBOneVideo
                : L.AetherStream.SwitchOneVideo));
            return;
        }

        if (!watchAlong.SendLoad(link, channel))
        {
            return;
        }

        linkInput = string.Empty;
        addSheet.Close();
        ShowWatch(channel);
    }

    private void SubmitInput(string raw) => SubmitInput(raw, addMode);

    private void SubmitInput(string raw, QueueAddMode mode)
    {
        if (watchAlong.IsViewing && mode == QueueAddMode.PlayNow && watchAlong.CanSwitchVideo)
        {
            SwitchPartyVideo(raw, VideoChannel.A);
            return;
        }

        var input = MediaInput.Normalize(raw);
        if (input.Length == 0)
        {
            return;
        }

        var localPath = MediaInput.LooksLikeLocalPath(input);
        if (!localPath && !VideoEngine.ValidateURL(input, out _))
        {
            ShellToast.Show(Loc.T(L.AetherStream.NotALink));
            return;
        }

        linkInput = string.Empty;
        if (watchAlong.IsViewing)
        {
            if (localPath)
            {
                ShellToast.Show(Loc.T(L.AetherStream.SuggestLinksOnly));
                return;
            }

            watchAlong.SuggestQueueItem(input);
            ShellToast.Show(Loc.T(watchAlong.CanAddDirectly
                ? L.AetherStream.AddedToQueueToast
                : L.AetherStream.SuggestionSent));
            addSheet.Close();
            return;
        }

        if (!localPath && VideoUrlResolver.IsPlaylistUrl(input))
        {
            if (VideoUrlResolver.NamesOneVideo(input))
            {
                actionLink = input;
                linkInput = input;
                pendingPlaylistMode = mode;
                BeginActions(SheetPurpose.PlaylistLink, Loc.T(L.AetherStream.PlaylistLinkTitle));
                AddAction(PlaylistActionOne, Loc.T(L.AetherStream.PlaylistJustThis));
                AddAction(PlaylistActionAll, Loc.T(L.AetherStream.PlaylistWhole));
                addSheet.Close();
                actions.Open();
                return;
            }

            StartPlaylistImport(input, mode);
            return;
        }

        AddSingle(input, mode);
    }

    private void AddSingle(string input, QueueAddMode mode)
    {
        if (mode == QueueAddMode.PlayNow)
        {
            CancelPendingJoin();
        }

        queue.Insert(queue.CreateDisplayEntry(input), mode);
        addSheet.Close();
        if (mode == QueueAddMode.PlayNow)
        {
            ShowWatch(VideoChannel.A);
            return;
        }

        ShellToast.Show(Loc.T(mode == QueueAddMode.PlayNext
            ? L.AetherStream.PlayingNextToast
            : L.AetherStream.AddedToQueueToast));
    }

    private void StartPlaylistImport(string input, QueueAddMode mode)
    {
        if (mode == QueueAddMode.PlayNow)
        {
            CancelPendingJoin();
        }

        if (!queue.ImportPlaylist(input, mode, true))
        {
            linkInput = input;
            ShellToast.Show(Loc.T(L.AetherStream.PlaylistImporting));
            return;
        }

        addSheet.Close();
        ShellToast.Show(Loc.T(L.AetherStream.PlaylistImporting));
        if (mode == QueueAddMode.PlayNow)
        {
            ShowWatch(VideoChannel.A);
        }
    }

    private void HandlePlaylistLinkAction(int code)
    {
        if (actionLink.Length == 0)
        {
            return;
        }

        var link = actionLink;
        actionLink = string.Empty;
        linkInput = string.Empty;
        if (code == PlaylistActionAll)
        {
            StartPlaylistImport(link, pendingPlaylistMode);
            return;
        }

        AddSingle(link, pendingPlaylistMode);
    }

    private void PollCopiedLink()
    {
        if (!clipboardCadence.Advance(ImGui.GetIO().DeltaTime, ClipboardPollSeconds))
        {
            return;
        }

        clipboardCadence.Reset();
        if (!ClipboardWatch.HasChanged(ref clipboardSequence))
        {
            return;
        }

        var clipboard = MediaInput.Normalize(ImGui.GetClipboardText());
        if (string.Equals(clipboard, copiedLink, StringComparison.Ordinal))
        {
            return;
        }

        copiedLink = string.Empty;
        copiedLinkHost = string.Empty;
        if (clipboard.Length is 0 or > MaxLinkLength
            || !clipboard.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || !VideoEngine.ValidateURL(clipboard, out var parsed) || parsed is null)
        {
            return;
        }

        copiedLink = clipboard;
        copiedLinkHost = parsed.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? parsed.Host[4..]
            : parsed.Host;
    }
}
