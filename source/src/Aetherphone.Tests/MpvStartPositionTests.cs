using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MpvStartPositionTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(0.6d)]
    [InlineData(-3d)]
    public void APlayFromTheTopLeavesTheStartUnsetSoALiveStreamOpensAtItsLiveEdge(double startSeconds)
    {
        Assert.Equal("none", MpvRenderer.StartOption(startSeconds));
    }

    [Theory]
    [InlineData(1d, "1")]
    [InlineData(1026.9d, "1026")]
    public void AResumeStartsAtTheWholeSecondItWasLeftAt(double startSeconds, string expected)
    {
        Assert.Equal(expected, MpvRenderer.StartOption(startSeconds));
    }
}
