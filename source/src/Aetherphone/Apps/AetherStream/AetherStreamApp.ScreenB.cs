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
    private TextCache screenBLabel;
    private TextCache screenBTime;
    private readonly string[] screenChannelLabels = new string[2];
    private VideoChannel editedScreen = VideoChannel.A;

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

    private void DrawMainScreenChoice(float scale)
    {
        FillScreenChannelLabels();
        Gap(Metrics.Space.Md);
        SectionLabel(Loc.T(L.AetherStream.MainScreen));
        var picked = (VideoChannel)DrawChoiceStrip("aetherstream.mainScreen", screenChannelLabels,
            (int)configuration.VideoMainChannel, Loc.T(L.AetherStream.MainScreenHint), scale);
        if (picked == configuration.VideoMainChannel)
        {
            return;
        }

        configuration.VideoMainChannel = picked;
        configuration.Save();
    }

    private void DrawScreenBSection(float scale)
    {
        if (watchAlong.IsHosting && watchAlong.ViewerFailuresB.Count > 0)
        {
            DrawViewerFailuresBCard(scale);
        }

        var playerB = suite.PlayerB;
        var failed = playerB.State == VideoPlaybackState.Failed;
        var stalled = playerB.RecoveryExhausted;
        if (suite.ChannelB.Current is null && !playerB.HasMedia && !failed)
        {
            return;
        }

        if (failed || stalled)
        {
            DrawScreenBFailureCard(scale, stalled && !failed);
        }
        else
        {
            DrawScreenBCard(scale);
        }

        if (watchAlong.IsViewing && watchAlong.HostScreenBOutOfReach)
        {
            Gap(Metrics.Space.Sm);
            DrawNote(Loc.T(L.AetherStream.HostScreenBFarAway), scale);
        }
    }

    private void DrawScreenBCard(float scale)
    {
        var playerB = suite.PlayerB;
        var current = suite.ChannelB.Current;
        var viewing = watchAlong.IsViewing;
        var canPause = playerB.HasMedia && (!viewing || watchAlong.CanControlPlayback);
        var canStop = !viewing;
        Gap(Metrics.Space.Md);
        var pad = Metrics.Space.Md * scale;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var timeHeight = Typography.LineHeight(TextStyles.Caption1);
        var textHeight = labelHeight + titleHeight + timeHeight;
        var card = BeginBlock(MathF.Max(UpNextCardHeight * scale, textHeight + pad * 1.4f));
        var drawList = ImGui.GetWindowDrawList();
        GlassCard(drawList, card);
        var thumbHeight = card.Height - pad * 1.4f;
        var thumbMin = new Vector2(card.Min.X + pad * 0.7f, card.Center.Y - thumbHeight * 0.5f);
        var thumb = new Rect(thumbMin, thumbMin + new Vector2(thumbHeight * ThumbAspect, thumbHeight));
        DrawThumb(drawList, thumb, current?.Url, current?.ThumbnailUrl, Metrics.Radius.Sm * scale);

        var radius = RoundButton.SmallRadius * scale;
        var buttonRight = card.Max.X - pad;
        var textRight = buttonRight;
        var stopCenter = new Vector2(buttonRight - radius, card.Center.Y);
        if (canStop)
        {
            buttonRight = stopCenter.X - radius - Metrics.Space.Sm * scale;
            textRight = stopCenter.X - radius - pad;
        }

        var pauseCenter = new Vector2(buttonRight - radius, card.Center.Y);
        if (canPause)
        {
            textRight = pauseCenter.X - radius - pad;
        }

        var textLeft = thumb.Max.X + pad;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var top = card.Center.Y - textHeight * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(screenBLabel.Upper(Loc.T(L.AetherStream.ScreenB)), textWidth,
                TextStyles.FootnoteEmphasized), Ink.AccentLink, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + labelHeight),
            Typography.FitText(current?.Title ?? Loc.T(L.AetherStream.NothingPlaying), textWidth,
                TextStyles.BodyEmphasized), Ink.TitleInk, TextStyles.BodyEmphasized);
        var timeText = playerB.State == VideoPlaybackState.Loading
            ? Loc.T(L.AetherStream.LoadingVideo)
            : ScreenBTimeText(playerB.Progress);
        Typography.Draw(drawList, new Vector2(textLeft, top + labelHeight + titleHeight),
            Typography.FitText(timeText, textWidth, TextStyles.Caption1), Ink.MutedInk, TextStyles.Caption1);

        if (canPause && StreamRoundButton(drawList, "aetherstream.screenB.pause", pauseCenter, radius,
                playerB.Progress.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause, ButtonStyle.Gray))
        {
            ToggleScreenBPlayback();
        }

        if (canStop && StreamRoundButton(drawList, "aetherstream.screenB.stop", stopCenter, radius,
                FontAwesomeIcon.Stop, ButtonStyle.Gray, Loc.T(L.AetherStream.StopScreenB)))
        {
            suite.ChannelB.Stop();
        }

        EndBlock();
    }

    private string ScreenBTimeText(PlaybackProgress progress)
    {
        var position = TimeText.MinutesSeconds((int)MathF.Max(0f, progress.Position));
        if (progress.Duration <= 0f)
        {
            return position;
        }

        return screenBTime.Pair(position, TimeText.MinutesSeconds((int)progress.Duration));
    }

    private void ToggleScreenBPlayback()
    {
        var paused = suite.PlayerB.Progress.Paused;
        if (watchAlong.IsViewing)
        {
            watchAlong.ControlPause(!paused, VideoChannel.B);
            return;
        }

        suite.PlayerB.Pause(!paused);
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
        suite.ChannelB.Play(current.Url, resume);
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
