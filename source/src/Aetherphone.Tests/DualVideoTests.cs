using System.Numerics;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DualVideoTests
{
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
    [InlineData((byte)VideoChannel.B, 0, 60)]
    public void WithoutSpatialAudioOnlyTheMainScreenIsHeard(byte main, int volumeA, int volumeB)
    {
        var volumes = DualVideo.Volumes(60, spatialAudio: false, (VideoChannel)main, playingA: true, playingB: true);

        Assert.Equal(new ChannelVolumes(volumeA, volumeB), volumes);
    }

    [Fact]
    public void WithSpatialAudioBothScreensAreHeard()
    {
        var volumes = DualVideo.Volumes(60, spatialAudio: true, VideoChannel.A, playingA: true, playingB: true);

        Assert.Equal(new ChannelVolumes(60, 60), volumes);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ALoneScreenIsHeardEvenWhenItIsNotTheMainOne(bool playingA, bool playingB)
    {
        var main = playingA ? VideoChannel.B : VideoChannel.A;

        var volumes = DualVideo.Volumes(60, spatialAudio: false, main, playingA, playingB);

        Assert.Equal(new ChannelVolumes(60, 60), volumes);
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
