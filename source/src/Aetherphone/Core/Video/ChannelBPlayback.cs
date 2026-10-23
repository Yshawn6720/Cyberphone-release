namespace Aetherphone.Core.Video;

internal sealed class ChannelBPlayback : IDisposable
{
    private readonly VideoPlayer player;
    private readonly VideoEngine engine;
    private readonly AetherStreamQueue queue;
    private readonly VideoLibrary library;
    private bool remembered;

    internal ChannelBPlayback(VideoPlayer player, VideoEngine engine, AetherStreamQueue queue, VideoLibrary library)
    {
        this.player = player;
        this.engine = engine;
        this.queue = queue;
        this.library = library;
        player.Finished += OnFinished;
    }

    internal event Action? Changed;

    internal VideoQueueEntry? Current { get; private set; }

    internal VideoPlayer Player => player;

    internal VideoEngine Engine => engine;

    internal void Play(string url, double startSeconds = 0d, bool playing = true)
    {
        if (url.Length == 0)
        {
            return;
        }

        Play(queue.CreateDisplayEntry(url), startSeconds, playing);
    }

    internal void Play(VideoQueueEntry entry, double startSeconds = 0d, bool playing = true)
    {
        NotePosition();
        queue.EnrichIfYouTube(entry);
        Current = entry;
        remembered = true;
        library.NotePlayed(entry);
        player.Play(entry.Url, startSeconds, playing);
        Changed?.Invoke();
    }

    internal void Follow(string url, double startSeconds, bool playing)
    {
        NotePosition();
        if (Current is not { } current || !string.Equals(current.Url, url, StringComparison.Ordinal))
        {
            Current = queue.CreateDisplayEntry(url);
        }

        remembered = false;
        player.Play(url, startSeconds, playing);
    }

    internal void Adopt()
    {
        if (remembered || Current is not { } current)
        {
            return;
        }

        remembered = true;
        library.NotePlayed(current);
        Changed?.Invoke();
    }

    internal void Stop()
    {
        if (Current is null && !player.HasMedia && player.State != VideoPlaybackState.Failed)
        {
            return;
        }

        NotePosition();
        Current = null;
        remembered = false;
        player.Stop();
        Changed?.Invoke();
    }

    private void NotePosition()
    {
        if (!remembered || Current is not { } current
            || player.State is not (VideoPlaybackState.Playing or VideoPlaybackState.Paused))
        {
            return;
        }

        var progress = player.Progress;
        library.NotePosition(current.Url, progress.Position, progress.Duration);
    }

    private void OnFinished()
    {
        if (!remembered || Current is not { } finished)
        {
            return;
        }

        library.NoteFinished(finished.Url);
        remembered = false;
        Stop();
    }

    public void Dispose()
    {
        NotePosition();
        player.Finished -= OnFinished;
    }
}
