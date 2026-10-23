namespace Aetherphone.Core.Video;

internal sealed class PartyAutoReplay
{
    private const long InitialDelayMilliseconds = 10 * 1000;
    private const long MaxDelayMilliseconds = 60 * 1000;
    private const long BotCheckDelayMilliseconds = 5 * 60 * 1000;
    private const long BotCheckMaxDelayMilliseconds = 15 * 60 * 1000;

    private long delayMilliseconds;
    private long nextAtTicks;

    internal string? Url { get; private set; }

    internal float InSeconds(long now) => Url is null ? 0f : Math.Max(0f, (nextAtTicks - now) / 1000f);

    internal void Clear() => Url = null;

    internal bool Due(VideoPlaybackState state, PlaybackFailureKind failureKind, string? failedUrl, long now)
    {
        if (Url is not null && state == VideoPlaybackState.Playing)
        {
            Url = null;
        }

        if (state != VideoPlaybackState.Failed || failedUrl is null)
        {
            return false;
        }

        if (PlaybackFailureClassifier.RetryCannotHelp(failureKind))
        {
            Url = null;
            return false;
        }

        var botCheck = failureKind == PlaybackFailureKind.BotCheck;
        if (Url != failedUrl)
        {
            Url = failedUrl;
            delayMilliseconds = botCheck ? BotCheckDelayMilliseconds : InitialDelayMilliseconds;
            nextAtTicks = now + delayMilliseconds;
            return false;
        }

        if (botCheck && delayMilliseconds < BotCheckDelayMilliseconds)
        {
            delayMilliseconds = BotCheckDelayMilliseconds;
            nextAtTicks = now + delayMilliseconds;
            return false;
        }

        if (now < nextAtTicks)
        {
            return false;
        }

        var maxDelay = botCheck ? BotCheckMaxDelayMilliseconds : MaxDelayMilliseconds;
        delayMilliseconds = Math.Min(delayMilliseconds * 2, maxDelay);
        nextAtTicks = now + delayMilliseconds;
        return true;
    }
}
