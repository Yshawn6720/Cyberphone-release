using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StuckWaitTests
{
    [Fact]
    public void ASeekOrBufferThatNeverEndsCountsAsStalledAfterTheBufferingWindow()
    {
        const long since = 5_000;

        Assert.False(StuckWait.Stalled(since, since + VideoEngine.BufferingWatchdogMilliseconds - 1_000));
        Assert.True(StuckWait.Stalled(since, since + VideoEngine.BufferingWatchdogMilliseconds));
        Assert.False(StuckWait.Stalled(null, long.MaxValue));
    }

    [Fact]
    public void APlayingLinkWaitsWhileItSeeksOrBuffers()
    {
        Assert.True(StuckWait.Waiting(active: true, loading: false, recovering: false, paused: false,
            hasPosition: true, seeking: true, coreIdle: false));
        Assert.True(StuckWait.Waiting(active: true, loading: false, recovering: false, paused: false,
            hasPosition: true, seeking: false, coreIdle: true));
        Assert.False(StuckWait.Waiting(active: true, loading: false, recovering: false, paused: false,
            hasPosition: true, seeking: false, coreIdle: false));
    }

    [Fact]
    public void APausedLoadingRestartingOrUnresolvedPlayerIsNeverWaiting()
    {
        Assert.False(StuckWait.Waiting(active: true, loading: false, recovering: false, paused: true,
            hasPosition: true, seeking: false, coreIdle: true));
        Assert.False(StuckWait.Waiting(active: true, loading: true, recovering: false, paused: false,
            hasPosition: true, seeking: true, coreIdle: true));
        Assert.False(StuckWait.Waiting(active: true, loading: false, recovering: true, paused: false,
            hasPosition: true, seeking: true, coreIdle: true));
        Assert.False(StuckWait.Waiting(active: true, loading: false, recovering: false, paused: false,
            hasPosition: false, seeking: false, coreIdle: true));
        Assert.False(StuckWait.Waiting(active: false, loading: false, recovering: false, paused: false,
            hasPosition: true, seeking: true, coreIdle: true));
    }
}
