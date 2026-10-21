using Aetherphone.Core.Media;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GifFramePlanTests
{
    [Fact]
    public void SmallAnimationsKeepEveryFrame()
    {
        Assert.Equal(1, GifFramePlan.KeepEvery(30, 480, 270, 9_000_000L));
    }

    [Fact]
    public void OversizedAnimationsDecimateToFitThePixelBudget()
    {
        var keepEvery = GifFramePlan.KeepEvery(300, 480, 480, 9_000_000L);

        Assert.True(keepEvery > 1);
        var keptFrames = (300 + keepEvery - 1) / keepEvery;
        Assert.True((long)keptFrames * 480 * 480 <= 9_000_000L + 480L * 480L);
    }

    [Fact]
    public void DecimationPreservesTheTotalDuration()
    {
        var rawDelays = new float[100];
        for (var index = 0; index < rawDelays.Length; index++)
        {
            rawDelays[index] = 0.05f;
        }

        var (keptIndices, delays) = GifFramePlan.Plan(rawDelays, 480, 480, 9_000_000L);

        Assert.True(keptIndices.Length < rawDelays.Length);
        var total = 0f;
        for (var index = 0; index < delays.Length; index++)
        {
            total += delays[index];
        }

        Assert.Equal(5f, total, 3);
    }

    [Fact]
    public void ZeroDelaysFallBackToTheBrowserPace()
    {
        Assert.Equal(GifFramePlan.SlowFallbackDelaySeconds, GifFramePlan.NormalizeDelay(0f));
        Assert.Equal(GifFramePlan.SlowFallbackDelaySeconds, GifFramePlan.NormalizeDelay(0.01f));
        Assert.Equal(0.05f, GifFramePlan.NormalizeDelay(0.05f));
    }

    [Fact]
    public void FrameIndexWalksDelaysAndWraps()
    {
        var delays = new[] { 0.1f, 0.2f, 0.3f };
        const float total = 0.6f;

        Assert.Equal(0, GifFramePlan.FrameIndexAt(delays, total, 0.05));
        Assert.Equal(1, GifFramePlan.FrameIndexAt(delays, total, 0.15));
        Assert.Equal(2, GifFramePlan.FrameIndexAt(delays, total, 0.45));
        Assert.Equal(0, GifFramePlan.FrameIndexAt(delays, total, 0.65));
        Assert.Equal(2, GifFramePlan.FrameIndexAt(delays, total, 1.19));
    }

    [Fact]
    public void SingleFrameAnimationsAlwaysShowFrameZero()
    {
        Assert.Equal(0, GifFramePlan.FrameIndexAt(new[] { 0.1f }, 0.1f, 12.34));
        Assert.Equal(0, GifFramePlan.FrameIndexAt(new[] { 0f, 0f }, 0f, 12.34));
    }
}
