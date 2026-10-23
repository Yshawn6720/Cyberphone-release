using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private IReadOnlyList<ViewerFailure>? viewerFailureNamesSourceB;
    private string viewerFailureNamesB = string.Empty;
    private TextCache viewerFailureTitleB;
    private TextCache retryTextB;
    private readonly string[] screenChannelLabels = new string[2];
    private readonly string[] soundChoiceLabels = new string[3];
    private VideoChannel editedScreen = VideoChannel.A;
    private VideoChannel watchScreen = VideoChannel.A;
    private VideoChannel playerActionsScreen = VideoChannel.A;
    private bool screenTabsShown;

    private bool ScreenBPresent => suite.ChannelB.Current is not null || suite.PlayerB.HasMedia
        || suite.PlayerB.State == VideoPlaybackState.Failed;

    private VideoPlayer PlayerFor(VideoChannel channel) => channel == VideoChannel.B ? suite.PlayerB : video;

    private VideoQueueEntry? EntryFor(VideoChannel channel) =>
        channel == VideoChannel.B ? suite.ChannelB.Current : CurrentEntry;

    private void ShowWatch(VideoChannel channel)
    {
        activeTab = StreamTab.Watch;
        watchScreen = channel;
        screenTabsShown = true;
    }

    private void FillScreenChannelLabels()
    {
        screenChannelLabels[0] = Loc.T(L.AetherStream.ScreenA);
        screenChannelLabels[1] = Loc.T(L.AetherStream.ScreenB);
    }

    private void DrawScreenPicker(float scale)
    {
        FillScreenChannelLabels();
        var picked = (VideoChannel)DrawChoiceStrip("aetherstream.screenPicker", screenChannelLabels,
            (int)editedScreen, string.Empty, scale);
        Gap(Metrics.Space.Md);
        if (picked == editedScreen)
        {
            return;
        }

        editedScreen = picked;
        stageDrag = StageDrag.None;
        nudgeHeldKey = -1;
    }

    private bool PlacingScreenFor(VideoChannel channel) =>
        channel == VideoChannel.B ? suite.PlacingScreenB : suite.PlacingScreen;

    private void SetPlacingScreen(VideoChannel channel, bool placing)
    {
        if (channel == VideoChannel.B)
        {
            suite.PlacingScreenB = placing;
            return;
        }

        suite.PlacingScreen = placing;
    }

    private void DrawPlaceBeside(bool live, float scale)
    {
        Gap(Metrics.Space.Md);
        var row = BeginBlock(ActionTileHeight * scale);
        var engineB = screen.EngineB;
        if (ActionTile(row, FontAwesomeIcon.Columns, Loc.T(L.AetherStream.PlaceBesideScreenA), false, live, scale))
        {
            engineB.PlaceBesideCompanion();
            suite.PlacingScreenB = false;
            stageDrag = StageDrag.None;
        }

        EndBlock();
    }

    private void DrawScreenTabs(float scale)
    {
        FillScreenChannelLabels();
        var stripHeight = SegmentHeight * scale;
        var captionGap = Metrics.Space.Xs * scale;
        var captionHeight = Typography.LineHeight(TextStyles.Caption1);
        var block = BeginBlock(stripHeight + captionGap + captionHeight);
        var strip = new Rect(block.Min, new Vector2(block.Max.X, block.Min.Y + stripHeight));
        var picked = (VideoChannel)SegmentStrip.Draw("aetherstream.watchScreen", strip, screenChannelLabels,
            (int)watchScreen, ui.Palette);
        var drawList = ImGui.GetWindowDrawList();
        var volumes = suite.Volumes;
        var captionTop = strip.Max.Y + captionGap;
        DrawScreenTabCaption(drawList, SegmentStrip.SegmentRect(strip, 0, 2), captionTop, VideoChannel.A, volumes.A,
            scale);
        DrawScreenTabCaption(drawList, SegmentStrip.SegmentRect(strip, 1, 2), captionTop, VideoChannel.B, volumes.B,
            scale);
        EndBlock();
        Gap(Metrics.Space.Md);
        watchScreen = picked;
    }

    private void DrawScreenTabCaption(ImDrawListPtr drawList, Rect segment, float top, VideoChannel channel,
        int volume, float scale)
    {
        var player = PlayerFor(channel);
        var status = DualVideo.Status(player.State, player.Progress.Paused, player.RecoveryExhausted);
        var text = Loc.T(status switch
        {
            ScreenStatus.Loading => L.AetherStream.LoadingVideo,
            ScreenStatus.Playing => L.AetherStream.ScreenPlaying,
            ScreenStatus.Paused => L.AetherStream.ScreenPaused,
            ScreenStatus.Failed => L.AetherStream.ScreenCantPlay,
            _ => L.AetherStream.NothingPlaying,
        });
        var style = TextStyles.Caption1;
        var lineHeight = Typography.LineHeight(style);
        var iconSize = lineHeight * 0.9f;
        var iconSpace = player.HasMedia ? iconSize + Metrics.Space.Xs * scale : 0f;
        var fitted = Typography.FitText(text,
            MathF.Max(1f, segment.Width - Metrics.Space.Sm * scale - iconSpace), style);
        var left = segment.Center.X - (iconSpace + Typography.Measure(fitted, style).X) * 0.5f;
        if (player.HasMedia)
        {
            var heard = volume > 0;
            ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, top + lineHeight * 0.5f),
                heard ? FontAwesomeIcon.VolumeUp : FontAwesomeIcon.VolumeMute,
                heard ? Ink.AccentLink : Ink.MutedInk, iconSize);
        }

        Typography.Draw(drawList, new Vector2(left + iconSpace, top), fitted,
            status == ScreenStatus.Failed ? theme.Danger : Ink.MutedInk, style);
    }

    private void DrawSound(VideoChannel channel, float scale)
    {
        if (suite.BothScreensPlaying)
        {
            soundChoiceLabels[0] = Loc.T(L.AetherStream.ScreenA);
            soundChoiceLabels[1] = Loc.T(L.AetherStream.ScreenB);
            soundChoiceLabels[2] = Loc.T(L.AetherStream.PlayModeBoth);
            var sound = suite.Sound;
            var choice = DualVideo.SoundChoice(sound);
            Gap(Metrics.Space.Md);
            SectionLabel(Loc.T(L.AetherStream.ScreenSound));
            var picked = (ScreenSoundChoice)DrawChoiceStrip("aetherstream.screenSound", soundChoiceLabels,
                (int)choice,
                Loc.T(sound.BothScreens ? L.AetherStream.ScreenSoundBothHint : L.AetherStream.ScreenSoundOneHint),
                scale);
            if (picked != choice)
            {
                suite.PickSound(picked);
            }
        }

        DrawScreenVolume(channel, scale);
    }

    private void DrawScreenVolume(VideoChannel channel, float scale)
    {
        Gap(Metrics.Space.Xs);
        var row = BeginBlock(26f * scale);
        var isB = channel == VideoChannel.B;
        var volumes = suite.Volumes;
        var silent = (isB ? volumes.B : volumes.A) == 0;
        var muted = isB ? configuration.VideoMuteScreenB : configuration.VideoMuteScreenA;
        var volume = isB ? configuration.VideoVolumeB : configuration.VideoVolume;
        var result = VolumeSlider.Draw(isB ? "aetherstream.volumeB" : "aetherstream.volumeA", row, volume,
            accentedTheme, icon: silent ? FontAwesomeIcon.VolumeMute : null);
        var icon = VolumeSlider.IconArea(row);
        var iconHovered = UiInteract.Hover(icon.Min, icon.Max);
        if (iconHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(icon.Min, icon.Max, iconHovered))
        {
            suite.TapSpeaker(channel);
        }

        if (result.Dragging && Math.Abs(result.Value - volume) > 0.001f)
        {
            SetScreenVolume(isB, result.Value);
            if (muted)
            {
                SetScreenMuted(isB, false);
            }
        }

        if (result.Released)
        {
            SetScreenVolume(isB, result.Value);
            configuration.Save();
        }

        EndBlock();
    }

    private void SetScreenVolume(bool screenB, float volume)
    {
        if (screenB)
        {
            configuration.VideoVolumeB = volume;
            return;
        }

        configuration.VideoVolume = volume;
    }

    private void SetScreenMuted(bool screenB, bool muted)
    {
        if (screenB)
        {
            configuration.VideoMuteScreenB = muted;
            return;
        }

        configuration.VideoMuteScreenA = muted;
    }

    private void DrawScreenBNotices(float scale)
    {
        DrawViewerFailuresB(scale);

        var playerB = suite.PlayerB;
        var failed = playerB.State == VideoPlaybackState.Failed;
        var stalled = playerB.RecoveryExhausted;
        if (failed || stalled)
        {
            DrawScreenBFailureCard(scale, stalled && !failed);
        }

        if (watchAlong.IsViewing && watchAlong.HostScreenBOutOfReach)
        {
            Gap(Metrics.Space.Sm);
            DrawNote(Loc.T(L.AetherStream.HostScreenBFarAway), scale);
        }
    }

    private void DrawViewerFailuresB(float scale)
    {
        if (watchAlong.IsHosting && watchAlong.ViewerFailuresB.Count > 0)
        {
            DrawViewerFailuresBCard(scale);
        }
    }

    private void DrawScreenWall(bool live, float scale)
    {
        Gap(Metrics.Space.Sm);
        var gap = Metrics.Space.Sm * scale;
        var row = BeginBlock(ActionTileHeight * scale);
        var tileWidth = (row.Width - gap) * 0.5f;
        var sideBySide = ActionTile(new Rect(row.Min, new Vector2(row.Min.X + tileWidth, row.Max.Y)),
            FontAwesomeIcon.ArrowsAltH, Loc.T(L.AetherStream.WallSideBySide), false, live, scale);
        var stacked = ActionTile(new Rect(new Vector2(row.Min.X + tileWidth + gap, row.Min.Y), row.Max),
            FontAwesomeIcon.ArrowsAltV, Loc.T(L.AetherStream.WallStacked), false, live, scale);
        EndBlock();
        Gap(Metrics.Space.Sm);
        DrawNote(Loc.T(L.AetherStream.WallHint), scale);
        if (sideBySide || stacked)
        {
            PlaceScreenWall(stacked ? ScreenWallLayout.Stacked : ScreenWallLayout.SideBySide);
        }
    }

    private void PlaceScreenWall(ScreenWallLayout layout)
    {
        if (!screen.PlaceScreenWall(layout))
        {
            return;
        }

        suite.PlacingScreen = false;
        suite.PlacingScreenB = false;
        stageDrag = StageDrag.None;
        configuration.VideoScreenCurve = screen.Engine.ScreenCurve;
        if (watchAlong.IsViewing)
        {
            configuration.VideoFollowHostScreen = false;
        }

        configuration.Save();
    }

    private void ToggleScreenBPlayback()
    {
        var playerB = suite.PlayerB;
        var paused = playerB.Progress.Paused;
        if (watchAlong.IsViewing)
        {
            watchAlong.ControlPause(!paused, VideoChannel.B);
            return;
        }

        if (playerB.HasMedia)
        {
            playerB.Pause(!paused);
            return;
        }

        if (playerB.State == VideoPlaybackState.Failed)
        {
            RetryScreenB(false);
        }
    }

    private void DrawScreenBFailureCard(float scale, bool stalled)
    {
        var viewing = watchAlong.IsViewing;
        var footnote = RetryFootnote(ref retryTextB, viewing ? watchAlong.AutoReplayBInSeconds : 0f);
        string reason;
        if (stalled)
        {
            reason = Loc.T(L.AetherStream.FailureStalledBody);
        }
        else if (!TryDescribeFailureKind(suite.PlayerB.FailureKind, out reason, out _))
        {
            reason = Loc.T(L.AetherStream.PlaybackFailed);
        }

        var canRetry = viewing || suite.ChannelB.Current is not null;
        var stopLabel = viewing ? null : Loc.T(L.AetherStream.Stop);
        var title = Loc.T(stalled ? L.AetherStream.FailureStalledTitle : L.AetherStream.ScreenBFailedTitle);
        DrawActionCard("aetherstream.screenB.failure", scale, theme.Danger, title, reason, footnote,
            canRetry ? Loc.T(L.AetherStream.FailureRetry) : Loc.T(L.AetherStream.Stop), canRetry ? stopLabel : null,
            out var primary, out var secondary);
        if (primary && canRetry)
        {
            RetryScreenB(stalled);
            return;
        }

        if (primary || secondary)
        {
            suite.ChannelB.Stop();
        }
    }

    private void RetryScreenB(bool resumeFromPosition)
    {
        if (watchAlong.IsViewing)
        {
            watchAlong.RetryChannelB();
            return;
        }

        if (suite.ChannelB.Current is not { } current)
        {
            return;
        }

        var resume = resumeFromPosition ? (double)suite.PlayerB.Progress.Position : 0d;
        suite.PlayerB.ResetRecoveryBudget();
        suite.ChannelB.Play(current, resume);
    }

    private void DrawViewerFailuresBCard(float scale)
    {
        var failures = watchAlong.ViewerFailuresB;
        if (!ReferenceEquals(failures, viewerFailureNamesSourceB))
        {
            viewerFailureNamesSourceB = failures;
            viewerFailureNamesB = JoinViewerNames(failures);
        }

        var watchers = Math.Max(failures.Count, watchAlong.Roster.Count - 1);
        var title = viewerFailureTitleB.Format(Loc.T(L.AetherStream.FailureViewersBTitle), failures.Count, watchers);
        DrawActionCard("aetherstream.screenB.viewerFailures", scale, ui.Accent, title, viewerFailureNamesB,
            Loc.T(L.AetherStream.FailureViewersBHint), Loc.T(L.AetherStream.StopScreenB),
            Loc.T(L.AetherStream.FailureDismiss), out var stop, out var dismiss);
        if (stop)
        {
            watchAlong.DismissViewerFailuresB();
            suite.ChannelB.Stop();
            return;
        }

        if (dismiss)
        {
            watchAlong.DismissViewerFailuresB();
        }
    }
}
