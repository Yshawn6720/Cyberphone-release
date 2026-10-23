using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PartyAutoReplayTests
{
    private const string Url = "https://youtu.be/abc";
    private const long Start = 1_000_000;
    private const long Second = 1000;

    [Fact]
    public void AFailedLinkIsRetriedWithADoublingWaitCappedAtAMinute()
    {
        var replay = new PartyAutoReplay();

        Assert.False(replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, Url, Start));
        Assert.Equal(10f, replay.InSeconds(Start));

        var retries = new List<long>();
        for (var now = Start; now <= Start + 200 * Second; now += Second)
        {
            if (replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, Url, now))
            {
                retries.Add((now - Start) / Second);
            }
        }

        Assert.Equal([10L, 30L, 70L, 130L, 190L], retries);
    }

    [Fact]
    public void ABotCheckWaitsFiveMinutesAndBacksOffToFifteen()
    {
        var replay = new PartyAutoReplay();
        replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.BotCheck, Url, Start);

        Assert.Equal(300f, replay.InSeconds(Start));
        Assert.False(replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.BotCheck, Url, Start + 299 * Second));
        Assert.True(replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.BotCheck, Url, Start + 300 * Second));
        Assert.Equal(600f, replay.InSeconds(Start + 300 * Second));
        Assert.True(replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.BotCheck, Url, Start + 900 * Second));
        Assert.Equal(900f, replay.InSeconds(Start + 900 * Second));
    }

    [Fact]
    public void AFailureThatTurnsIntoABotCheckRestartsTheLongerWait()
    {
        var replay = new PartyAutoReplay();
        replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, Url, Start);

        Assert.False(replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.BotCheck, Url, Start + 10 * Second));
        Assert.Equal(300f, replay.InSeconds(Start + 10 * Second));
    }

    [Theory]
    [InlineData((byte)PlaybackFailureKind.DrmProtected)]
    [InlineData((byte)PlaybackFailureKind.RegionLocked)]
    [InlineData((byte)PlaybackFailureKind.LoginRequired)]
    [InlineData((byte)PlaybackFailureKind.UnsupportedSite)]
    public void AFailureARetryCannotFixIsNeverRetried(byte kind)
    {
        var replay = new PartyAutoReplay();

        for (var now = Start; now <= Start + 120 * Second; now += Second)
        {
            Assert.False(replay.Due(VideoPlaybackState.Failed, (PlaybackFailureKind)kind, Url, now));
        }

        Assert.Null(replay.Url);
        Assert.Equal(0f, replay.InSeconds(Start));
    }

    [Fact]
    public void PlayingAgainResetsTheWaitForTheNextFailure()
    {
        var replay = new PartyAutoReplay();
        replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, Url, Start);
        replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, Url, Start + 10 * Second);

        Assert.False(replay.Due(VideoPlaybackState.Playing, PlaybackFailureKind.Unknown, Url, Start + 11 * Second));
        Assert.Null(replay.Url);

        replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, Url, Start + 20 * Second);
        Assert.Equal(10f, replay.InSeconds(Start + 20 * Second));
    }

    [Fact]
    public void ANewLinkFailingStartsItsOwnShortWait()
    {
        var replay = new PartyAutoReplay();
        replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, Url, Start);
        replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, Url, Start + 10 * Second);

        Assert.False(replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, "https://youtu.be/next",
            Start + 12 * Second));
        Assert.Equal(10f, replay.InSeconds(Start + 12 * Second));
    }

    [Fact]
    public void NothingIsScheduledWithoutAFailedLink()
    {
        var replay = new PartyAutoReplay();

        Assert.False(replay.Due(VideoPlaybackState.Failed, PlaybackFailureKind.Unknown, null, Start));
        Assert.False(replay.Due(VideoPlaybackState.Idle, PlaybackFailureKind.Unknown, Url, Start));
        Assert.Null(replay.Url);
    }
}
