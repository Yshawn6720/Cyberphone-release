using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DroppedStreamTests
{
    [Fact]
    public void ALinkWhoseDownloadDroppedMidVideoEndedEarly()
    {
        Assert.True(DroppedStream.EndedEarly("https://youtu.be/abc", 23.033d, 60d));
        Assert.True(DroppedStream.EndedEarly("http://127.0.0.1:8080/clip.mp4", 0d, 60d));
    }

    [Fact]
    public void ALinkPlayedToItsLastSecondsFinished()
    {
        const string url = "https://youtu.be/abc";

        Assert.False(DroppedStream.EndedEarly(url, 59.933d, 60d));
        Assert.False(DroppedStream.EndedEarly(url, 600d - DroppedStream.EndGuardSeconds, 600d));
        Assert.True(DroppedStream.EndedEarly(url, 600d - DroppedStream.EndGuardSeconds - 0.1d, 600d));
    }

    [Theory]
    [InlineData("C:\\Videos\\show.mkv")]
    [InlineData("D:/clip.mp4")]
    [InlineData("")]
    [InlineData(null)]
    public void ALocalFileThatEndsShortIsNotReopened(string? url)
    {
        Assert.False(DroppedStream.EndedEarly(url, 23d, 60d));
    }

    [Fact]
    public void AStreamWithoutAKnownLengthFinishesWhenItEnds()
    {
        Assert.False(DroppedStream.EndedEarly("https://youtu.be/live", 3600d, 0d));
    }
}
