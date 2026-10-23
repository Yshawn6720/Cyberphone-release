using System.Numerics;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DualVideoTests
{
    private static readonly ScreenSound ScreenAPicked = new(VideoChannel.A, BothScreens: false, Volume: 60,
        VolumeB: 40, MutedA: false, MutedB: false);

    [Theory]
    [InlineData((byte)VideoPlayMode.Both, true, true)]
    [InlineData((byte)VideoPlayMode.A, true, false)]
    [InlineData((byte)VideoPlayMode.B, false, true)]
    public void AMemberWhoFollowsTheHostSeesTheHostsDefault(byte hostMode, bool showA, bool showB)
    {
        var view = DualVideo.View(VideoWatchChoice.FollowHost, (VideoPlayMode)hostMode, hasA: true, hasB: true);

        Assert.Equal(new ChannelView(showA, showB), view);
    }

    [Fact]
    public void AHostWithoutAPlayModeShowsBothScreens()
    {
        var view = DualVideo.View(VideoWatchChoice.FollowHost, null, hasA: true, hasB: true);

        Assert.Equal(new ChannelView(true, true), view);
    }

    [Theory]
    [InlineData((byte)VideoWatchChoice.A, true, false)]
    [InlineData((byte)VideoWatchChoice.B, false, true)]
    [InlineData((byte)VideoWatchChoice.Both, true, true)]
    public void AMembersOwnChoiceOverridesTheHostsDefault(byte choice, bool showA, bool showB)
    {
        var view = DualVideo.View((VideoWatchChoice)choice, VideoPlayMode.A, hasA: true, hasB: true);

        Assert.Equal(new ChannelView(showA, showB), view);
    }

    [Fact]
    public void PickingTheScreenTheRoomLacksStillShowsTheOtherOne()
    {
        Assert.Equal(new ChannelView(true, false),
            DualVideo.View(VideoWatchChoice.B, VideoPlayMode.B, hasA: true, hasB: false));
        Assert.Equal(new ChannelView(false, true),
            DualVideo.View(VideoWatchChoice.A, VideoPlayMode.A, hasA: false, hasB: true));
    }

    [Fact]
    public void ARoomWithNeitherVideoShowsNothing()
    {
        Assert.Equal(new ChannelView(false, false),
            DualVideo.View(VideoWatchChoice.Both, VideoPlayMode.Both, hasA: false, hasB: false));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, false)]
    public void OffAPartyOrHostingTheShownScreensAreTheOnesPlaying(bool playingA, bool playingB, bool showA,
        bool showB)
    {
        Assert.Equal(new ChannelView(showA, showB), DualVideo.LocalView(playingA, playingB));
    }

    [Fact]
    public void AHostPlayingOnlyScreenBSeesTheirOwnDanmakuOnB()
    {
        var view = DualVideo.LocalView(playingA: false, playingB: true);

        Assert.Equal(VideoChannel.B, DualVideo.WatchedScreen(view, VideoChannel.A));
    }

    [Theory]
    [InlineData((byte)VideoChannel.A, 60, 0)]
    [InlineData((byte)VideoChannel.B, 0, 40)]
    public void ByDefaultOnlyTheMainScreenIsHeardAtItsOwnVolume(byte main, int volumeA, int volumeB)
    {
        var sound = ScreenAPicked with { MainChannel = (VideoChannel)main };

        Assert.Equal(new ChannelVolumes(volumeA, volumeB), DualVideo.Volumes(sound, liveA: true, liveB: true));
    }

    [Fact]
    public void PickingScreenBMovesTheSoundToBAndSilencesAInOneStep()
    {
        var picked = DualVideo.Pick(ScreenAPicked, ScreenSoundChoice.B);

        Assert.Equal(ScreenSoundChoice.B, DualVideo.SoundChoice(picked));
        Assert.Equal(new ChannelVolumes(0, 40), DualVideo.Volumes(picked, liveA: true, liveB: true));
        var back = DualVideo.Pick(picked, ScreenSoundChoice.A);
        Assert.Equal(new ChannelVolumes(60, 0), DualVideo.Volumes(back, liveA: true, liveB: true));
    }

    [Fact]
    public void BothScreensPlayTogetherEachAtItsOwnVolumeAndMute()
    {
        var both = DualVideo.Pick(ScreenAPicked, ScreenSoundChoice.Both);

        Assert.Equal(ScreenSoundChoice.Both, DualVideo.SoundChoice(both));
        Assert.Equal(new ChannelVolumes(60, 40), DualVideo.Volumes(both, liveA: true, liveB: true));
        Assert.Equal(new ChannelVolumes(0, 40),
            DualVideo.Volumes(both with { MutedA = true }, liveA: true, liveB: true));
        Assert.Equal(new ChannelVolumes(60, 0),
            DualVideo.Volumes(both with { MutedB = true }, liveA: true, liveB: true));
    }

    [Fact]
    public void AMutedMainScreenStaysSilentWhenOnlyOneIsHeard()
    {
        var muted = ScreenAPicked with { MainChannel = VideoChannel.B, MutedB = true };

        Assert.Equal(new ChannelVolumes(0, 0), DualVideo.Volumes(muted, liveA: true, liveB: true));
    }

    [Fact]
    public void TurningBothOffReturnsToTheScreenPickedBefore()
    {
        var onB = DualVideo.Pick(ScreenAPicked, ScreenSoundChoice.B);

        var both = DualVideo.Pick(onB, ScreenSoundChoice.Both);

        Assert.Equal(VideoChannel.B, both.MainChannel);
        Assert.Equal(new ChannelVolumes(0, 40),
            DualVideo.Volumes(both with { BothScreens = false }, liveA: true, liveB: true));
    }

    [Theory]
    [InlineData(true, false, (byte)VideoChannel.B, false)]
    [InlineData(false, true, (byte)VideoChannel.A, false)]
    [InlineData(true, false, (byte)VideoChannel.A, true)]
    [InlineData(false, true, (byte)VideoChannel.B, true)]
    public void ALoneScreenIsHeardAtItsOwnVolumeWhateverWasPicked(bool liveA, bool liveB, byte main, bool both)
    {
        var sound = ScreenAPicked with { MainChannel = (VideoChannel)main, BothScreens = both };

        Assert.Equal(new ChannelVolumes(60, 40), DualVideo.Volumes(sound, liveA, liveB));
        Assert.Equal(new ChannelVolumes(0, 0),
            DualVideo.Volumes(sound with { MutedA = true, MutedB = true }, liveA, liveB));
    }

    [Fact]
    public void WhenTheHeardScreenStopsTheOtherTakesOverAndItGetsTheSoundBackWhenItPlaysAgain()
    {
        var onB = DualVideo.Pick(ScreenAPicked, ScreenSoundChoice.B);

        Assert.Equal(new ChannelVolumes(60, 40), DualVideo.Volumes(onB, liveA: true, liveB: false));
        Assert.Equal(new ChannelVolumes(0, 40), DualVideo.Volumes(onB, liveA: true, liveB: true));
    }

    [Fact]
    public void TappingTheSpeakerOfTheHeardScreenMutesOnlyThatScreen()
    {
        var tapped = DualVideo.TapSpeaker(ScreenAPicked, VideoChannel.A, bothLive: true);

        Assert.True(tapped.MutedA);
        Assert.False(tapped.MutedB);
        Assert.Equal(VideoChannel.A, tapped.MainChannel);
        Assert.Equal(new ChannelVolumes(0, 0), DualVideo.Volumes(tapped, liveA: true, liveB: true));
        var again = DualVideo.TapSpeaker(tapped, VideoChannel.A, bothLive: true);
        Assert.Equal(new ChannelVolumes(60, 0), DualVideo.Volumes(again, liveA: true, liveB: true));
    }

    [Fact]
    public void TappingTheSpeakerOfTheSilentScreenMovesTheSoundToIt()
    {
        var tapped = DualVideo.TapSpeaker(ScreenAPicked with { MutedB = true }, VideoChannel.B, bothLive: true);

        Assert.Equal(VideoChannel.B, tapped.MainChannel);
        Assert.False(tapped.MutedB);
        Assert.Equal(new ChannelVolumes(0, 40), DualVideo.Volumes(tapped, liveA: true, liveB: true));
    }

    [Fact]
    public void TappingASpeakerWhileBothAreHeardTogglesOnlyThatScreen()
    {
        var both = DualVideo.Pick(ScreenAPicked, ScreenSoundChoice.Both);

        var tapped = DualVideo.TapSpeaker(both, VideoChannel.B, bothLive: true);

        Assert.True(tapped.BothScreens);
        Assert.Equal(VideoChannel.A, tapped.MainChannel);
        Assert.Equal(new ChannelVolumes(60, 0), DualVideo.Volumes(tapped, liveA: true, liveB: true));
    }

    [Fact]
    public void TappingTheSpeakerOfALoneScreenMutesItWithoutMovingTheSound()
    {
        var tapped = DualVideo.TapSpeaker(ScreenAPicked, VideoChannel.B, bothLive: false);

        Assert.True(tapped.MutedB);
        Assert.Equal(VideoChannel.A, tapped.MainChannel);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, true)]
    public void ScreenTabsShowWhenScreenBPlaysOrCanBeStarted(bool viewing, bool hasA, bool hasB, bool shown)
    {
        Assert.Equal(shown, DualVideo.Tabs(VideoChannel.A, true, viewing, hasA, hasB).Shown);
    }

    [Fact]
    public void HiddenTabsFallBackToScreenA()
    {
        Assert.Equal(new ScreenTabs(false, VideoChannel.A),
            DualVideo.Tabs(VideoChannel.B, wasShown: true, viewing: true, hasA: true, hasB: false));
    }

    [Fact]
    public void TabsThatJustAppearedOpenOnTheScreenThatPlays()
    {
        Assert.Equal(VideoChannel.B,
            DualVideo.Tabs(VideoChannel.A, wasShown: false, viewing: false, hasA: false, hasB: true).Screen);
        Assert.Equal(VideoChannel.A,
            DualVideo.Tabs(VideoChannel.B, wasShown: false, viewing: true, hasA: true, hasB: true).Screen);
    }

    [Fact]
    public void TabsAlreadyShownKeepThePickedScreenEvenWhenItIsEmpty()
    {
        Assert.Equal(VideoChannel.B,
            DualVideo.Tabs(VideoChannel.B, wasShown: true, viewing: false, hasA: true, hasB: false).Screen);
        Assert.Equal(VideoChannel.A,
            DualVideo.Tabs(VideoChannel.A, wasShown: true, viewing: false, hasA: false, hasB: true).Screen);
    }

    [Fact]
    public void AViewerWhoseScreenAStopsMovesToTheScreenBThatStillPlays()
    {
        Assert.Equal(new ScreenTabs(true, VideoChannel.B),
            DualVideo.Tabs(VideoChannel.A, wasShown: true, viewing: true, hasA: false, hasB: true));
    }

    [Theory]
    [InlineData((byte)VideoPlaybackState.Idle, false, false, (byte)ScreenStatus.Idle)]
    [InlineData((byte)VideoPlaybackState.Loading, false, false, (byte)ScreenStatus.Loading)]
    [InlineData((byte)VideoPlaybackState.Playing, false, false, (byte)ScreenStatus.Playing)]
    [InlineData((byte)VideoPlaybackState.Playing, true, false, (byte)ScreenStatus.Paused)]
    [InlineData((byte)VideoPlaybackState.Paused, false, false, (byte)ScreenStatus.Paused)]
    [InlineData((byte)VideoPlaybackState.Playing, false, true, (byte)ScreenStatus.Failed)]
    [InlineData((byte)VideoPlaybackState.Failed, false, false, (byte)ScreenStatus.Failed)]
    public void ATabNamesWhatItsScreenIsDoing(byte state, bool paused, bool exhausted, byte expected)
    {
        Assert.Equal((ScreenStatus)expected, DualVideo.Status((VideoPlaybackState)state, paused, exhausted));
    }

    [Theory]
    [InlineData(true, true, (byte)VideoChannel.B, (byte)VideoChannel.B)]
    [InlineData(true, true, (byte)VideoChannel.A, (byte)VideoChannel.A)]
    [InlineData(false, true, (byte)VideoChannel.A, (byte)VideoChannel.B)]
    [InlineData(true, false, (byte)VideoChannel.B, (byte)VideoChannel.A)]
    public void TheWatchedScreenIsTheMainOneOnlyWhileBothShow(bool showA, bool showB, byte main, byte expected)
    {
        Assert.Equal((VideoChannel)expected, DualVideo.WatchedScreen(new ChannelView(showA, showB), (VideoChannel)main));
    }

    [Fact]
    public void DanmakuTypedOnAScreenTabGoesToThatScreenWhileItShows()
    {
        var both = new ChannelView(true, true);

        Assert.Equal(VideoChannel.B, DualVideo.DanmakuTarget(both, VideoChannel.A, VideoChannel.B));
        Assert.Equal(VideoChannel.A, DualVideo.DanmakuTarget(both, VideoChannel.B, VideoChannel.A));
        Assert.Equal(VideoChannel.B, DualVideo.DanmakuTarget(both, VideoChannel.B, null));
        Assert.Equal(VideoChannel.A,
            DualVideo.DanmakuTarget(new ChannelView(true, false), VideoChannel.A, VideoChannel.B));
    }

    [Fact]
    public void DanmakuLandsOnTheScreenItsSenderWatches()
    {
        var both = new ChannelView(true, true);

        Assert.Equal(VideoChannel.B, DualVideo.DanmakuScreen(DualVideo.WireB, both));
        Assert.Equal(VideoChannel.A, DualVideo.DanmakuScreen(DualVideo.WireA, both));
    }

    [Fact]
    public void DanmakuForAScreenThisMemberHidesMovesToTheShownOne()
    {
        Assert.Equal(VideoChannel.A, DualVideo.DanmakuScreen(DualVideo.WireB, new ChannelView(true, false)));
        Assert.Equal(VideoChannel.B, DualVideo.DanmakuScreen(DualVideo.WireA, new ChannelView(false, true)));
    }

    [Theory]
    [InlineData(null, true, true, (byte)VideoChannel.A)]
    [InlineData(null, false, true, (byte)VideoChannel.B)]
    [InlineData("x", true, true, (byte)VideoChannel.A)]
    public void DanmakuFromAnOlderPluginGoesToScreenAUnlessOnlyBShows(string? screen, bool showA, bool showB,
        byte expected)
    {
        Assert.Equal((VideoChannel)expected, DualVideo.DanmakuScreen(screen, new ChannelView(showA, showB)));
    }

    [Theory]
    [InlineData(StreamFeature.Party | StreamFeature.Danmaku | StreamFeature.DualVideo, true)]
    [InlineData(StreamFeature.Party | StreamFeature.DualVideo, true)]
    [InlineData(StreamFeature.Party | StreamFeature.Danmaku, false)]
    [InlineData(StreamFeature.DualVideo, false)]
    [InlineData(0, false)]
    public void ScreenBNeedsARelayThatAdvertisesBothPartyAndDualVideo(int features, bool supported)
    {
        Assert.Equal(supported, DualVideo.Supported(features));
    }

    [Theory]
    [InlineData("", (byte)ScreenBLink.Empty)]
    [InlineData("hello there", (byte)ScreenBLink.NotALink)]
    [InlineData("https://www.youtube.com/playlist?list=PLFgquLnL59alCl_2TQvOiD5Vgm1hCaGSI",
        (byte)ScreenBLink.Playlist)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLFgquLnL59alCl_2TQvOiD5Vgm1hCaGSI",
        (byte)ScreenBLink.Playable)]
    [InlineData("youtube.com/watch?v=dQw4w9WgXcQ", (byte)ScreenBLink.Playable)]
    [InlineData("C:\\Videos\\show.mkv", (byte)ScreenBLink.Playable)]
    public void ScreenBTakesOneVideoOrALocalFileButNotAWholePlaylist(string input, byte expected)
    {
        Assert.Equal((ScreenBLink)expected, DualVideo.CheckScreenBLink(input));
    }

    [Fact]
    public void WireNamesRoundTripAndUnknownValuesFallBack()
    {
        Assert.Equal(VideoPlayMode.A, DualVideo.ParsePlayMode(DualVideo.PlayModeWire(VideoPlayMode.A)));
        Assert.Equal(VideoPlayMode.B, DualVideo.ParsePlayMode(DualVideo.PlayModeWire(VideoPlayMode.B)));
        Assert.Equal(VideoPlayMode.Both, DualVideo.ParsePlayMode(DualVideo.PlayModeWire(VideoPlayMode.Both)));
        Assert.Null(DualVideo.ParsePlayMode("c"));
        Assert.Null(DualVideo.ParsePlayMode(null));
        Assert.Equal(VideoChannel.B, DualVideo.ParseChannel(DualVideo.ChannelWire(VideoChannel.B)));
        Assert.Equal(VideoChannel.A, DualVideo.ParseChannel(null));
        Assert.Equal(VideoChannel.A, DualVideo.ParseChannel("B"));
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1.2f, 2.5f)]
    [InlineData(-2.9f, 0.6f)]
    public void ScreenBSitsOnScreenAsRightWithAGapAndNoOverlap(float yaw, float scale)
    {
        var pose = new ScreenPose(new Vector3(10f, 2f, -4f), yaw, 0f, 0f, scale);

        var beside = ScreenGeometry.Beside(pose);

        var offset = beside - pose.Position;
        var width = 2f * ScreenGeometry.HalfWidth * scale;
        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(offset), ScreenGeometry.Right(pose)), 4);
        var gap = offset.Length() - width;
        Assert.Equal(width * ScreenGeometry.CompanionGapShare, gap, 4);
    }
}
