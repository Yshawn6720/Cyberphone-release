using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

internal readonly record struct ChannelHostReport(StreamChannelState? State, bool Changed, bool ScreenChanged);

internal sealed class PartyChannelB : IDisposable
{
    private readonly ChannelBPlayback playback;
    private readonly StreamSignalRouter stream;
    private readonly Configuration configuration;
    private readonly ServerClock serverClock;
    private readonly PlaybackSyncController sync = new();
    private readonly PartyAutoReplay autoReplay = new();
    private readonly PartyResyncBudget resyncBudget = new();

    private string? viewingUrl;
    private string? reportedFailureUrl;
    private CallControl? lastState;
    private bool shown;
    private double viewingDurationSeconds;
    private bool playbackEnded;

    private string publishedUrl = string.Empty;
    private double publishedPosition;
    private DateTime publishedAt = DateTime.UtcNow;
    private bool publishedPaused;
    private StreamScreenPose? publishedScreen;

    internal PartyChannelB(ChannelBPlayback playback, StreamSignalRouter stream, Configuration configuration,
        ServerClock serverClock)
    {
        this.playback = playback;
        this.stream = stream;
        this.configuration = configuration;
        this.serverClock = serverClock;
        playback.Player.Finished += OnFinished;
    }

    internal bool ScreenOutOfReach { get; private set; }

    internal float AutoReplayInSeconds => autoReplay.InSeconds(Environment.TickCount64);

    internal string PublishedUrl => publishedUrl;

    internal void Apply(CallControl message, bool show, bool force)
    {
        lastState = message;
        shown = show;
        var url = show && message.UrlB is { Length: > 0 } remote && VideoEngine.ValidateURL(remote, out _)
            ? remote
            : null;
        if (url is null)
        {
            if (viewingUrl is not null)
            {
                StopViewing();
            }

            return;
        }

        if (url != viewingUrl || force)
        {
            StartViewing(url, message);
        }
        else if (message.PausedB is { } paused && playback.Player.HasMedia)
        {
            playback.Player.Pause(paused);
        }

        ApplyScreen(message);
    }

    internal void Retry()
    {
        autoReplay.Clear();
        playback.Player.ResetRecoveryBudget();
        if (lastState is { } message)
        {
            Apply(message, shown, force: true);
        }
    }

    private void StartViewing(string url, CallControl message)
    {
        sync.Reset();
        viewingDurationSeconds = 0d;
        playbackEnded = false;
        reportedFailureUrl = null;
        viewingUrl = url;
        playback.Follow(url, ProjectPosition(message), !(message.PausedB ?? false));
    }

    internal void StopViewing()
    {
        var wasViewing = viewingUrl is not null;
        viewingUrl = null;
        lastState = null;
        reportedFailureUrl = null;
        playbackEnded = false;
        ScreenOutOfReach = false;
        autoReplay.Clear();
        Release();
        sync.Reset();
        if (wasViewing)
        {
            playback.Stop();
        }
    }

    internal void Step(float deltaSeconds)
    {
        if (viewingUrl is not { } url)
        {
            return;
        }

        var player = playback.Player;
        if (player.State == VideoPlaybackState.Failed && reportedFailureUrl != url)
        {
            reportedFailureUrl = url;
            stream.ReportPlaybackFailure(url, player.LastError, DualVideo.WireB);
        }

        if (autoReplay.Due(player.State, player.FailureKind, lastState is null ? null : url, Environment.TickCount64)
            && lastState is { } replayState)
        {
            playback.Follow(url, ProjectPosition(replayState), !(replayState.PausedB ?? false));
        }

        ReopenIfEndedEarly(url);
        if (player.State is VideoPlaybackState.Playing or VideoPlaybackState.Paused && player.Progress.Duration > 0f)
        {
            viewingDurationSeconds = player.Progress.Duration;
        }

        if (lastState is not { } message
            || !PartyViewerSync.CanSync(player.State == VideoPlaybackState.Playing, message.PausedB ?? false,
                player.IsLiveUrl(url), player.IsRestarting, player.PositionUnavailable)
            || message.PositionSecondsB is not { } position || message.StateAtUnixMs is not { } stamp)
        {
            Release();
            return;
        }

        var age = StateAgeSeconds(stamp);
        if (age > WatchAlongSession.StaleStateSeconds)
        {
            Release();
            return;
        }

        var progress = player.Progress;
        var target = Math.Max(0d, position + age);
        var decision = sync.Step(progress.Position, target, progress.Duration, progress.Seeking, deltaSeconds);
        if (decision.SpeedChanged)
        {
            player.SetSpeed(decision.Speed);
        }

        if (decision.Reopen)
        {
            ReopenToResync(url, message, progress.Position - target, decision.SeekTarget);
            return;
        }

        if (decision.Seek)
        {
            player.Seek(decision.SeekTarget);
        }
    }

    private void ReopenToResync(string url, CallControl message, double drift, double seekTarget)
    {
        if (!resyncBudget.TryTake(url, Environment.TickCount64))
        {
            AepLog.Warning($"[WatchAlong] Screen B sync drifted {drift:F1}s after repeated seeks (seek lead "
                + $"{sync.SeekLead:F1}s); {url} was reopened under a minute ago, seeking to {seekTarget:F1}s");
            playback.Player.Seek(seekTarget);
            return;
        }

        var resumeAt = ProjectPosition(message);
        AepLog.Warning($"[WatchAlong] Screen B sync drifted {drift:F1}s after repeated seeks (seek lead "
            + $"{sync.SeekLead:F1}s); reopening {url} at {resumeAt:F1}s");
        sync.Reset();
        playback.Follow(url, resumeAt, true);
    }

    private void ReopenIfEndedEarly(string url)
    {
        if (!playbackEnded || playback.Player.State != VideoPlaybackState.Idle || lastState is not { } message)
        {
            return;
        }

        var resumeAt = ProjectPosition(message);
        if (!PartyFollow.ShouldReopen(message.UrlB, url, viewingDurationSeconds, resumeAt,
                playback.Player.IsLiveUrl(url)))
        {
            return;
        }

        playbackEnded = false;
        playback.Follow(url, resumeAt, !(message.PausedB ?? false));
    }

    private void Release()
    {
        if (sync.Release())
        {
            playback.Player.SetSpeed(1d);
        }
    }

    private double StateAgeSeconds(long stampUnixMs)
    {
        var serverNow = serverClock.ServerNowUnixMs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return Math.Max(0d, (serverNow - stampUnixMs) / 1000d);
    }

    private double ProjectPosition(CallControl message)
    {
        if (message.PositionSecondsB is not { } position)
        {
            return 0d;
        }

        if ((message.PausedB ?? false) || message.StateAtUnixMs is not { } stamp)
        {
            return Math.Max(0d, position);
        }

        return Math.Max(0d, position + Math.Min(StateAgeSeconds(stamp), WatchAlongSession.StaleStateSeconds));
    }

    private void ApplyScreen(CallControl message)
    {
        if (message is not { ScreenXB: { } x, ScreenYB: { } y, ScreenZB: { } z })
        {
            return;
        }

        if (!configuration.VideoFollowHostScreen)
        {
            ScreenOutOfReach = false;
            return;
        }

        var position = new Vector3(x, y, z);
        if (Plugin.ObjectTable.LocalPlayer is { } localPlayer
            && Vector3.Distance(localPlayer.Position, position) > WatchAlongSession.HostScreenReach)
        {
            ScreenOutOfReach = true;
            return;
        }

        ScreenOutOfReach = false;
        var engine = playback.Engine;
        engine.ApplyRemoteScreenPose(
            new ScreenPose(position, message.ScreenYawB ?? 0f, message.ScreenPitchB ?? engine.ScreenPitch,
                message.ScreenRollB ?? engine.ScreenRoll, message.ScreenScaleB ?? 1f), message.ScreenCurveB);
    }

    private void OnFinished() => playbackEnded = viewingUrl is not null;

    internal ChannelHostReport HostReport()
    {
        var player = playback.Player;
        var url = playback.Current is { } current && player.HasMedia && VideoEngine.ValidateURL(current.Url, out _)
            ? current.Url
            : string.Empty;
        if (url.Length == 0)
        {
            return new ChannelHostReport(null, publishedUrl.Length > 0, false);
        }

        var progress = player.Progress;
        var paused = progress.Paused;
        var expected = publishedPaused
            ? publishedPosition
            : publishedPosition + (DateTime.UtcNow - publishedAt).TotalSeconds;
        var position = PartyHostPosition.Report(url, publishedUrl, player.IsRestarting, player.PositionUnavailable,
            progress.Position, expected);
        var jumped = url == publishedUrl && Math.Abs(position - expected) > WatchAlongSession.PositionJumpTolerance;
        var engine = playback.Engine;
        StreamScreenPose? screen = engine.IsActive
            ? new StreamScreenPose(engine.ScreenPosition, engine.ScreenYaw, engine.ScreenPitch, engine.ScreenRoll,
                engine.ScreenScale, engine.ScreenCurve)
            : null;
        var changed = url != publishedUrl || paused != publishedPaused || jumped;
        return new ChannelHostReport(new StreamChannelState(url, position, paused, screen), changed,
            WatchAlongSession.ScreenMoved(screen, publishedScreen));
    }

    internal void CommitHost(StreamChannelState? state)
    {
        publishedUrl = state?.Url ?? string.Empty;
        publishedPosition = state?.PositionSeconds ?? 0d;
        publishedAt = DateTime.UtcNow;
        publishedPaused = state?.Paused ?? false;
        publishedScreen = state?.Screen;
    }

    internal void ResetHost() => CommitHost(null);

    internal bool AcceptsFailure(string url) => publishedUrl.Length > 0 && url == publishedUrl;

    internal bool ApplyControl(string action, double? positionSeconds)
    {
        var player = playback.Player;
        switch (action)
        {
            case StreamControlAction.Play:
                if (player.HasMedia)
                {
                    player.Pause(false);
                }

                return true;
            case StreamControlAction.Pause:
                if (player.HasMedia)
                {
                    player.Pause(true);
                }

                return true;
            case StreamControlAction.Seek:
                if (player.HasMedia && positionSeconds is { } seconds && double.IsFinite(seconds))
                {
                    player.Seek(Math.Max(0d, seconds));
                }

                return true;
            default:
                return false;
        }
    }

    internal void Load(string url) => playback.Play(url);

    internal void AdoptAsHost(CallControl message, bool supported)
    {
        var inheritedUrl = supported && message.UrlB is { Length: > 0 } url && VideoEngine.ValidateURL(url, out _)
            ? url
            : null;
        var keepPlaying = inheritedUrl is not null && viewingUrl == inheritedUrl && playback.Player.HasMedia;
        var resumeAt = ProjectPosition(message);
        var paused = message.PausedB ?? false;
        viewingUrl = null;
        lastState = null;
        reportedFailureUrl = null;
        playbackEnded = false;
        autoReplay.Clear();
        Release();
        sync.Reset();
        ResetHost();
        if (keepPlaying)
        {
            playback.Adopt();
            return;
        }

        if (inheritedUrl is null)
        {
            playback.Stop();
            return;
        }

        playback.Play(inheritedUrl, resumeAt, !paused);
    }

    internal void HandOver(CallControl message)
    {
        ResetHost();
        sync.Reset();
        lastState = message;
        shown = true;
        var playing = playback.Current?.Url;
        viewingUrl = playback.Player.HasMedia && playing is not null && message.UrlB == playing ? playing : null;
        if (viewingUrl is null)
        {
            playback.Stop();
        }
    }

    public void Dispose()
    {
        playback.Player.Finished -= OnFinished;
    }
}
