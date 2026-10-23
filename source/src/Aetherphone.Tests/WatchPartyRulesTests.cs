using System.Numerics;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;
using Dalamud.Game.Text;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WatchPartyRulesTests
{
    [Theory]
    [InlineData("ABCDEF", "ABCDEF")]
    [InlineData("abc-def", "ABCDEF")]
    [InlineData(" abc def ", "ABCDEF")]
    [InlineData("a2c 4e6", "A2C4E6")]
    public void AnInviteCodeIsReadHoweverItWasTyped(string typed, string expected)
    {
        Assert.Equal(expected, PartyCode.Normalize(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCDE")]
    [InlineData("ABCDEFG")]
    [InlineData("ABC-DEF-GHI-JKL-MNO")]
    public void AnythingThatIsNotSixCharactersIsNotACode(string? typed)
    {
        Assert.Equal(string.Empty, PartyCode.Normalize(typed));
    }

    [Fact]
    public void AnInviteCodeIsShownInTwoGroups()
    {
        Assert.Equal("ABC DEF", PartyCode.Display("ABCDEF"));
        Assert.Equal("ABCD", PartyCode.Display("ABCD"));
    }

    [Fact]
    public void AMemberHoldsTheGuestPermissionsPlusTheirOwnGrant()
    {
        var members = new[]
        {
            new StreamMember("trusted", StreamPermission.ControlPlayback),
            new StreamMember("capable", StreamPermission.CanHost),
        };

        var trusted = PartyPermissions.Held(StreamPermission.AddToQueue, members, "trusted");
        Assert.True(PartyPermissions.Allows(trusted, StreamPermission.AddToQueue));
        Assert.True(PartyPermissions.Allows(trusted, StreamPermission.ControlPlayback));

        var plain = PartyPermissions.Held(StreamPermission.AddToQueue, members, "plain");
        Assert.True(PartyPermissions.Allows(plain, StreamPermission.AddToQueue));
        Assert.False(PartyPermissions.Allows(plain, StreamPermission.ControlPlayback));

        Assert.Equal(0, PartyPermissions.Held(0, null, "anyone"));
    }

    [Fact]
    public void AnIdlePartyStaysOpenForFiveMinutes()
    {
        Assert.Equal(0f, PartyIdle.RemainingSeconds(0, 50_000));
        Assert.False(PartyIdle.Expired(0, 50_000));

        Assert.Equal(300f, PartyIdle.RemainingSeconds(10_000, 10_000));
        Assert.Equal(240f, PartyIdle.RemainingSeconds(10_000, 70_000));
        Assert.False(PartyIdle.Expired(10_000, 309_999));
        Assert.True(PartyIdle.Expired(10_000, 310_000));
        Assert.Equal(0f, PartyIdle.RemainingSeconds(10_000, 999_000));
    }

    [Fact]
    public void AViewerWhoseStreamEndedMidVideoReopensItWhileTheHostPlaysOn()
    {
        const string url = "https://youtu.be/abc";

        Assert.True(PartyFollow.ShouldReopen(url, url, 1800d, 1026d, viewerLive: false));
    }

    [Fact]
    public void AViewerWhoFinishedJustAheadOfTheHostWaitsForTheNextVideo()
    {
        const string url = "https://youtu.be/abc";

        Assert.False(PartyFollow.ShouldReopen(url, url, 600d, 599.4d, viewerLive: false));
        Assert.False(PartyFollow.ShouldReopen(url, url, 600d, 600d - PartyFollow.EndGuardSeconds, viewerLive: false));
        Assert.True(PartyFollow.ShouldReopen(url, url, 600d, 600d - PartyFollow.EndGuardSeconds - 0.1d,
            viewerLive: false));
    }

    [Fact]
    public void AViewerFollowsAnotherVideoThroughTheRoomStateRatherThanReopening()
    {
        Assert.False(PartyFollow.ShouldReopen("https://youtu.be/next", "https://youtu.be/abc", 600d, 12d,
            viewerLive: false));
        Assert.False(PartyFollow.ShouldReopen(null, "https://youtu.be/abc", 600d, 12d, viewerLive: false));
    }

    [Fact]
    public void AStreamWithoutAKnownLengthIsReopenedWhileTheHostStillShowsIt()
    {
        const string url = "https://youtu.be/live";

        Assert.True(PartyFollow.ShouldReopen(url, url, 0d, 3600d, viewerLive: false));
    }

    [Fact]
    public void AViewerWhoseLiveStreamEndedReopensItAtTheLiveEdgeWhateverTheHostClockReads()
    {
        const string url = "https://youtu.be/live";

        Assert.True(PartyFollow.ShouldReopen(url, url, 3605d, 7290d, viewerLive: true));
        Assert.True(PartyFollow.ShouldReopen(url, url, 3605d, 3600d, viewerLive: true));
        Assert.False(PartyFollow.ShouldReopen("https://youtu.be/next", url, 3605d, 12d, viewerLive: true));
    }

    [Fact]
    public void AViewerFollowsTheHostOnlyWhileItsOwnPlayerIsSteadilyPlayingAVideo()
    {
        Assert.True(PartyViewerSync.CanSync(playing: true, hostPaused: false, live: false, restarting: false,
            positionUnavailable: false));
        Assert.False(PartyViewerSync.CanSync(playing: true, hostPaused: false, live: false, restarting: true,
            positionUnavailable: false));
        Assert.False(PartyViewerSync.CanSync(playing: true, hostPaused: false, live: false, restarting: false,
            positionUnavailable: true));
        Assert.False(PartyViewerSync.CanSync(playing: true, hostPaused: false, live: true, restarting: false,
            positionUnavailable: false));
        Assert.False(PartyViewerSync.CanSync(playing: true, hostPaused: true, live: false, restarting: false,
            positionUnavailable: false));
        Assert.False(PartyViewerSync.CanSync(playing: false, hostPaused: false, live: false, restarting: false,
            positionUnavailable: false));
    }

    [Fact]
    public void AViewerReopensTheSameLinkToResyncAtMostOnceAMinute()
    {
        const string url = "https://youtu.be/abc";
        var budget = new PartyResyncBudget();

        Assert.True(budget.TryTake(url, 1_000));
        Assert.False(budget.TryTake(url, 1_000 + PartyResyncBudget.WindowMilliseconds - 1));
        Assert.True(budget.TryTake("https://youtu.be/next", 2_000));
        Assert.True(budget.TryTake(url, 2_000 + PartyResyncBudget.WindowMilliseconds - 1));
        Assert.False(budget.TryTake(url, 2_000 + PartyResyncBudget.WindowMilliseconds));
        Assert.True(budget.TryTake(url, 2_000 + 2 * PartyResyncBudget.WindowMilliseconds));
    }

    [Fact]
    public void AHostRestartingItsPlayerKeepsReportingWhereTheVideoWas()
    {
        const string url = "https://youtu.be/abc";

        Assert.Equal(1026d, PartyHostPosition.Report(url, url, restarting: true, positionUnavailable: true, 0d, 1026d));
        Assert.Equal(1026d, PartyHostPosition.Report(url, url, restarting: true, positionUnavailable: false, 1000d,
            1026d));
    }

    [Fact]
    public void AHostWithoutAPositionOnTheSameLinkKeepsReportingWhereTheVideoWas()
    {
        const string url = "https://youtu.be/abc";

        Assert.Equal(1026d, PartyHostPosition.Report(url, url, restarting: false, positionUnavailable: true, 0d,
            1026d));
    }

    [Fact]
    public void AHostSeekOutsideARestartReportsTheNewPosition()
    {
        const string url = "https://youtu.be/abc";

        Assert.Equal(0d, PartyHostPosition.Report(url, url, restarting: false, positionUnavailable: false, 0d, 1026d));
        Assert.Equal(1500d, PartyHostPosition.Report(url, url, restarting: false, positionUnavailable: false, 1500d,
            1026d));
    }

    [Fact]
    public void AHostOnAnotherLinkReportsItsOwnPositionEvenWhileItHasNone()
    {
        Assert.Equal(0d, PartyHostPosition.Report("https://youtu.be/next", "https://youtu.be/abc", restarting: true,
            positionUnavailable: true, 0d, 1026d));
        Assert.Equal(0d, PartyHostPosition.Report("https://youtu.be/next", "https://youtu.be/abc", restarting: false,
            positionUnavailable: true, 0d, 1026d));
        Assert.Equal(0d, PartyHostPosition.Report("https://youtu.be/abc", null, restarting: false,
            positionUnavailable: true, 0d, 1026d));
    }

    [Theory]
    [InlineData("\"C:\\Videos\\show.mkv\"", "C:\\Videos\\show.mkv")]
    [InlineData("  'D:\\clip.mp4'  ", "D:\\clip.mp4")]
    [InlineData(" https://youtu.be/abc ", "https://youtu.be/abc")]
    [InlineData("\"\"", "")]
    [InlineData(null, "")]
    public void PastedInputLosesItsQuotesAndPadding(string? raw, string expected)
    {
        Assert.Equal(expected, MediaInput.Normalize(raw));
    }

    [Theory]
    [InlineData("C:\\Videos\\show.mkv", true)]
    [InlineData("d:/clips/intro.mp4", true)]
    [InlineData("\\\\nas\\share\\film.mkv", true)]
    [InlineData("https://youtu.be/abc", false)]
    [InlineData("youtube.com/watch?v=abc", false)]
    [InlineData("aep-local:1:abc:10:name", false)]
    [InlineData("C:", false)]
    public void ALocalPathIsToldApartFromALink(string input, bool expected)
    {
        Assert.Equal(expected, MediaInput.LooksLikeLocalPath(input));
    }

    [Fact]
    public void ARowSubtitleJoinsTheSourceAndTheLength()
    {
        Assert.Equal("Channel", MediaInput.Subtitle("Channel", null));
        Assert.Equal("Channel", MediaInput.Subtitle("Channel", 0d));
        Assert.StartsWith("Channel", MediaInput.Subtitle("Channel", 125d));
        Assert.EndsWith("2:05", MediaInput.Subtitle("Channel", 125d));
        Assert.Equal("2:05", MediaInput.Subtitle(string.Empty, 125d));
    }

    [Fact]
    public void SoundIsFullUpCloseAndGoneAtTheEdgeOfTheRange()
    {
        Assert.Equal(1f, SpatialVolume.Gain(0f, 40f));
        Assert.Equal(1f, SpatialVolume.Gain(10f, 40f));
        Assert.Equal(0f, SpatialVolume.Gain(40f, 40f));
        Assert.Equal(0f, SpatialVolume.Gain(500f, 40f));

        var near = SpatialVolume.Gain(15f, 40f);
        var far = SpatialVolume.Gain(30f, 40f);
        Assert.InRange(near, 0f, 1f);
        Assert.True(near > far);
        Assert.True(far > 0f);
    }

    [Fact]
    public void AScreenTurnedToFaceSomeonePointsAtThem()
    {
        var screen = new Vector3(10f, 2f, -4f);
        var viewer = new Vector3(13f, 2f, 0f);
        var yaw = ScreenGeometry.YawFacing(screen, viewer);
        var facing = ScreenGeometry.Facing(new ScreenPose(screen, yaw, 0f, 0f, 1f));
        var expected = Vector3.Normalize(viewer - screen);

        Assert.Equal(expected.X, facing.X, 3);
        Assert.Equal(expected.Y, facing.Y, 3);
        Assert.Equal(expected.Z, facing.Z, 3);
    }

    [Fact]
    public void ANudgeMovesAlongTheScreenNotAlongTheWorld()
    {
        var pose = new ScreenPose(new Vector3(1f, 1f, 1f), MathF.PI * 0.5f, 0f, 0f, 2f);
        var right = ScreenGeometry.Right(pose);
        var moved = ScreenGeometry.Nudge(pose, 0.25f, 0f, 0f);

        Assert.Equal(0.25f, Vector3.Distance(pose.Position, moved), 4);
        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(moved - pose.Position), right), 4);

        var closer = ScreenGeometry.Nudge(pose, 0f, 0f, 0.5f);
        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(closer - pose.Position), ScreenGeometry.Facing(pose)), 4);
    }

    [Fact]
    public void TheCornersSpanTheScaledScreen()
    {
        var pose = new ScreenPose(Vector3.Zero, 0f, 0f, 0f, 3f);
        var topLeft = ScreenGeometry.Corner(pose, -1f, 1f);
        var bottomRight = ScreenGeometry.Corner(pose, 1f, -1f);

        Assert.Equal(ScreenGeometry.HalfWidth * 2f * 3f, bottomRight.X - topLeft.X, 4);
        Assert.Equal(ScreenGeometry.HalfHeight * 2f * 3f, topLeft.Y - bottomRight.Y, 4);
    }

    [Fact]
    public void ADragAlongAnArmTurnsPixelsIntoWorldDistance()
    {
        var start = new Vector2(100f, 100f);
        var end = new Vector2(200f, 100f);

        Assert.Equal(0.7f, ScreenGeometry.AlongAxis(new Vector2(100f, 0f), start, end, 0.7f), 4);
        Assert.Equal(-0.35f, ScreenGeometry.AlongAxis(new Vector2(-50f, 30f), start, end, 0.7f), 4);
        Assert.Equal(0f, ScreenGeometry.AlongAxis(new Vector2(0f, 40f), start, end, 0.7f), 4);
        Assert.Equal(0f, ScreenGeometry.AlongAxis(new Vector2(40f, 0f), start, start, 0.7f), 4);
    }

    [Fact]
    public void APlaceIsKeyedByZoneWorldAndHouse()
    {
        Assert.Equal("339:54:12:30:0", ScreenPlaces.Key(339, 54, 12, 30, 0));
        Assert.NotEqual(ScreenPlaces.Key(339, 54, 12, 30, 0), ScreenPlaces.Key(339, 54, 12, 31, 0));
    }

    [Theory]
    [InlineData(10d, 600d, 0d)]
    [InlineData(120d, 600d, 120d)]
    [InlineData(585d, 600d, 0d)]
    [InlineData(120d, 0d, 120d)]
    public void ResumeSkipsTheVeryStartAndTheVeryEnd(double position, double duration, double expected)
    {
        Assert.Equal(expected, VideoLibrary.ResumeFrom(position, duration));
    }

    [Fact]
    public void HistoryKeepsTheNewestFirstAndCarriesTheResumePoint()
    {
        var configuration = new Configuration();
        var library = new VideoLibrary(configuration);
        var first = new VideoQueueEntry("https://example.com/one.mp4", "One", "Site", TimeSpan.FromMinutes(10), null);
        var second = new VideoQueueEntry("https://example.com/two.mp4", "Two", "Site", null, null);

        library.NotePlayed(first);
        library.NotePosition(first.Url, 200d, 600d);
        library.NotePlayed(second);
        library.NotePlayed(first);

        Assert.Equal(2, library.History.Count);
        Assert.Equal(first.Url, library.History[0].Url);
        Assert.Equal(200d, library.History[0].PositionSeconds);

        library.NoteFinished(first.Url);
        Assert.Equal(0d, library.History[0].PositionSeconds);
    }

    [Fact]
    public void AFileSharedByFingerprintStaysOutOfHistory()
    {
        var configuration = new Configuration();
        var library = new VideoLibrary(configuration);
        library.NotePlayed(new VideoQueueEntry("aep-local:1:abc:10:name", "Name", "Local file", null, null));

        Assert.Empty(library.History);
    }

    [Fact]
    public void AReactionRisesForItsLifetimeAndThenIsGone()
    {
        var reactions = new PartyReactions();
        reactions.Add(2, 1_000);
        reactions.Add(99, 1_000);

        var live = 0;
        foreach (var reaction in reactions.Active)
        {
            if (PartyReactions.TryProgress(reaction, 1_000 + PartyReactions.LifetimeMilliseconds / 2, out var progress))
            {
                live++;
                Assert.Equal(2, reaction.Kind);
                Assert.Equal(0.5f, progress, 2);
                Assert.InRange(reaction.Lane, 0f, 1f);
            }
        }

        Assert.Equal(1, live);
        foreach (var reaction in reactions.Active)
        {
            Assert.False(PartyReactions.TryProgress(reaction, 1_000 + PartyReactions.LifetimeMilliseconds, out _));
        }
    }

    [Fact]
    public void TracksAreOfferedOnlyWhenThereIsSomethingToChoose()
    {
        var single = new[]
        {
            new MediaTrack(1, MediaTrackKind.Video, string.Empty, string.Empty, "h264", true),
            new MediaTrack(1, MediaTrackKind.Audio, string.Empty, string.Empty, "aac", true),
        };
        var dubbed = new[]
        {
            new MediaTrack(1, MediaTrackKind.Audio, "Original", "jpn", "flac", true),
            new MediaTrack(2, MediaTrackKind.Audio, string.Empty, "eng", "aac", false),
            new MediaTrack(1, MediaTrackKind.Subtitle, "Signs", string.Empty, "ass", false),
        };

        Assert.False(MediaTracks.OffersChoice(single));
        Assert.True(MediaTracks.OffersChoice(dubbed));
        Assert.Equal(2, MediaTracks.Count(dubbed, MediaTrackKind.Audio));
        Assert.Equal("Original (JPN)", MediaTracks.Label(dubbed[0], "Track 1"));
        Assert.Equal("ENG", MediaTracks.Label(dubbed[1], "Track 2"));
        Assert.Equal("Signs", MediaTracks.Label(dubbed[2], "Track 1"));
        Assert.Equal("Track 1", MediaTracks.Label(single[1], "Track 1"));
    }

    [Fact]
    public void TrackKindsAreReadFromThePlayer()
    {
        Assert.Equal(MediaTrackKind.Audio, MediaTracks.KindOf("audio"));
        Assert.Equal(MediaTrackKind.Subtitle, MediaTracks.KindOf("sub"));
        Assert.Equal(MediaTrackKind.Video, MediaTracks.KindOf("video"));
        Assert.Null(MediaTracks.KindOf("attachment"));
        Assert.Null(MediaTracks.KindOf(null));
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLFgquLnL59alCl_2TQvOiD5Vgm1hCaGSI", true, false)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLFgquLnL59alCl_2TQvOiD5Vgm1hCaGSI", true, true)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", false, true)]
    [InlineData("https://example.com/video.mp4", false, false)]
    [InlineData("hello", false, false)]
    public void APlaylistLinkIsRecognized(string url, bool playlist, bool namesOneVideo)
    {
        Assert.Equal(playlist, VideoUrlResolver.IsPlaylistUrl(url));
        Assert.Equal(namesOneVideo, VideoUrlResolver.NamesOneVideo(url));
    }

    [Fact]
    public void ChatBubblesCarryOnlyTheChannelsThatWereChosen()
    {
        Assert.Equal(ScreenChatChannels.Say, ScreenChatChannels.Of(XivChatType.Say));
        Assert.Equal(ScreenChatChannels.Party, ScreenChatChannels.Of(XivChatType.Party));
        Assert.Equal(ScreenChatChannels.Party, ScreenChatChannels.Of(XivChatType.CrossParty));
        Assert.Equal(ScreenChatChannels.Party, ScreenChatChannels.Of(XivChatType.Alliance));
        Assert.Equal(ScreenChatChannels.FreeCompany, ScreenChatChannels.Of(XivChatType.FreeCompany));
        Assert.Equal(ScreenChatChannels.Shout, ScreenChatChannels.Of(XivChatType.Yell));
        Assert.Equal(0, ScreenChatChannels.Of(XivChatType.TellIncoming));
        Assert.Equal(0, ScreenChatChannels.Of(XivChatType.Echo));
        Assert.Equal(0, ScreenChatChannels.Default & ScreenChatChannels.Shout);
    }

    [Fact]
    public void ChatTextLosesGameIconGlyphsAndStaysShort()
    {
        var withIcon = "hello" + (char)0xE040 + " there";
        Assert.Equal("hello there", ScreenChatFeed.Clean(withIcon));
        Assert.Equal(string.Empty, ScreenChatFeed.Clean(null));
        Assert.Equal(140, ScreenChatFeed.Clean(new string('a', 400)).Length);
    }

    [Fact]
    public void AChatLineFadesOutOverItsLifetime()
    {
        var line = new ScreenChatLine("Y'shtola", "over here", 5_000);

        Assert.True(ScreenChatFeed.TryAge(line, 5_000 + ScreenChatFeed.LifetimeMilliseconds / 2, out var age));
        Assert.Equal(0.5f, age, 2);
        Assert.False(ScreenChatFeed.TryAge(line, 5_000 + ScreenChatFeed.LifetimeMilliseconds, out _));
        Assert.False(ScreenChatFeed.TryAge(default, 5_000, out _));
    }

    [Fact]
    public void ACurvedScreenBringsItsEdgesTowardTheViewer()
    {
        var pose = new ScreenPose(Vector3.Zero, 0f, 0f, 0f, 3f);

        Assert.Equal(0.24f * 3f, ScreenGeometry.Corner(pose, 1f, 1f, 0.24f).Z, 4);
        Assert.Equal(0.24f * 3f, ScreenGeometry.Corner(pose, -1f, -1f, 0.24f).Z, 4);
        Assert.Equal(0f, ScreenGeometry.Corner(pose, 0f, 1f, 0.24f).Z, 4);
        Assert.Equal(0f, ScreenGeometry.Corner(pose, 1f, 1f).Z, 4);
    }

    [Fact]
    public void TheRoomRulesReadFromSettingsAndNeverGrantHosting()
    {
        var configuration = new Configuration
        {
            VideoStreamApprovalRequired = true,
            VideoStreamDiscoverable = false,
            VideoPartyCodeEnabled = true,
            VideoPartyGuestsCanAdd = true,
            VideoPartyGuestsCanControl = false,
            VideoPartyDanmakuEnabled = false,
        };

        var policy = PartyPolicy.From(configuration);

        Assert.True(policy.ApprovalRequired);
        Assert.False(policy.Discoverable);
        Assert.True(policy.CodeEnabled);
        Assert.True(policy.GuestsCanAdd);
        Assert.False(policy.GuestsCanControl);
        Assert.False(policy.DanmakuEnabled);
        Assert.True(PartyPolicy.From(new Configuration()).DanmakuEnabled);

        var widened = policy.WithGuestPermission(StreamPermission.ControlPlayback, true);
        Assert.True(widened.GuestsCanControl);
        Assert.True(widened.GuestsCanAdd);
        Assert.False(widened.WithGuestPermission(StreamPermission.AddToQueue, false).GuestsCanAdd);
        Assert.Equal(0, PartyPolicy.GuestMask & StreamPermission.CanHost);
    }

    [Fact]
    public void ASavedQueueNeverTakesANameThatIsInUse()
    {
        var configuration = new Configuration();
        configuration.VideoPlaylists.Add(new VideoPlaylistRecord { Name = "Playlist 2" });
        configuration.VideoPlaylists.Add(new VideoPlaylistRecord { Name = "playlist 3" });
        var library = new VideoLibrary(configuration);

        Assert.Equal("Playlist 4",
            library.NextPlaylistName("Playlist {0}", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ADanmakuLineLosesInvisibleCharactersAndStaysWithinSixtyCharacters()
    {
        Assert.Equal("hi there", PartyDanmaku.Clean("\uFEFF  hi\u202E th\u0007ere\uE040\u200B "));
        Assert.Equal("ok", PartyDanmaku.Clean("o\uDB80\uDC00k\uD800"));
        Assert.Equal(string.Empty, PartyDanmaku.Clean("\u200B\uFEFF\uE040"));
        Assert.Equal(string.Empty, PartyDanmaku.Clean(null));

        var cut = PartyDanmaku.Clean(string.Concat(Enumerable.Repeat("\U0001F600", PartyDanmaku.MaxTextLength + 5)));
        Assert.Equal(PartyDanmaku.MaxTextLength, cut.EnumerateRunes().Count());
    }

    [Theory]
    [InlineData(StreamFeature.Party, true, false)]
    [InlineData(StreamFeature.Party | StreamFeature.Danmaku, true, true)]
    [InlineData(StreamFeature.Party | StreamFeature.Danmaku, false, false)]
    [InlineData(0, true, false)]
    public void DanmakuIsOfferedOnlyWhenTheRelayCarriesItAndTheHostAllowsIt(int features, bool roomEnabled,
        bool expected)
    {
        Assert.Equal(expected, PartyDanmaku.CanSend(features, roomEnabled));
    }

    [Theory]
    [InlineData("", false, 0, false, (byte)DanmakuRefusal.Empty)]
    [InlineData("hi", false, 0, false, (byte)DanmakuRefusal.NotInParty)]
    [InlineData("hi", true, StreamFeature.Party, false, (byte)DanmakuRefusal.Unsupported)]
    [InlineData("hi", true, StreamFeature.Party | StreamFeature.Danmaku, false, (byte)DanmakuRefusal.OffByHost)]
    [InlineData("hi", true, StreamFeature.Party | StreamFeature.Danmaku, true, (byte)DanmakuRefusal.None)]
    public void ADanmakuThatCannotGoOutNamesTheFirstReason(string line, bool inParty, int features, bool roomEnabled,
        byte expected)
    {
        Assert.Equal((DanmakuRefusal)expected, PartyDanmaku.Refusal(line, inParty, features, roomEnabled));
    }

    [Fact]
    public void EachDanmakuLineTakesTheTopmostFreeLaneAndTheRestWait()
    {
        var danmaku = new PartyDanmaku();
        for (var index = 0; index < 4; index++)
        {
            danmaku.Enqueue("User" + index, "Name", "line " + index);
        }

        danmaku.Release(1_000, 3, MeasureByLength);

        Assert.Equal([0, 1, 2], LanesOf(danmaku));
        Assert.Equal(1, danmaku.WaitingCount);

        danmaku.Release(1_001, 3, MeasureByLength);
        Assert.Equal(1, danmaku.WaitingCount);

        danmaku.Release(1_000 + PartyDanmaku.TravelMilliseconds / 2, 3, MeasureByLength);
        Assert.Equal(0, danmaku.WaitingCount);
        Assert.Equal([0, 0, 1, 2], LanesOf(danmaku));
    }

    [Fact]
    public void ALongFastLineWaitsUntilItCannotCatchTheShortLineAheadOfIt()
    {
        var danmaku = new PartyDanmaku();
        danmaku.Enqueue("Short", "N", "a");
        danmaku.Enqueue("Long", "N", new string('x', 40));

        danmaku.Release(1_000, 1, MeasureByLength);
        danmaku.Release(1_000 + PartyDanmaku.TravelMilliseconds * 3 / 10, 1, MeasureByLength);
        Assert.Equal(1, danmaku.WaitingCount);

        danmaku.Release(1_000 + PartyDanmaku.TravelMilliseconds * 6 / 10, 1, MeasureByLength);
        Assert.Equal(0, danmaku.WaitingCount);
    }

    [Fact]
    public void TheScreenHoldsFourLinesALaneAndTheQueueDropsItsOldestPastThirty()
    {
        var danmaku = new PartyDanmaku();
        for (var index = 0; index < PartyDanmaku.WaitingCapacity + 5; index++)
        {
            danmaku.Enqueue("User", "N", "line " + index);
        }

        Assert.Equal(PartyDanmaku.WaitingCapacity, danmaku.WaitingCount);
        for (var now = 1_000L; now < 1_000 + PartyDanmaku.TravelMilliseconds * 3 / 4; now += 100)
        {
            danmaku.Release(now, 1, MeasureNarrow);
        }

        Assert.Equal(PartyDanmaku.LinesPerLane, LanesOf(danmaku).Length);
        Assert.Equal(PartyDanmaku.WaitingCapacity - PartyDanmaku.LinesPerLane, danmaku.WaitingCount);
        Assert.Equal("line 5", FirstStarted(danmaku).Text);
    }

    [Fact]
    public void HidingSomeoneClearsTheirLinesOnScreenAndInTheQueue()
    {
        var danmaku = new PartyDanmaku();
        danmaku.Enqueue("Loud", "Loud", "one");
        danmaku.Enqueue("Calm", "Calm", "two");
        danmaku.Release(1_000, 1, MeasureByLength);
        danmaku.Enqueue("Loud", "Loud", "three");

        danmaku.Hide("Loud");

        Assert.Equal(1, danmaku.WaitingCount);
        danmaku.Release(1_000 + PartyDanmaku.TravelMilliseconds / 2, 1, MeasureByLength);
        foreach (var line in danmaku.Active)
        {
            if (line.StartedAtTicks != 0)
            {
                Assert.Equal("Calm", line.UserId);
            }
        }
    }

    [Fact]
    public void HidingSomeoneAfterTheQueueWrapsKeepsEveryoneElseInOrder()
    {
        var danmaku = new PartyDanmaku();
        for (var index = 0; index < PartyDanmaku.WaitingCapacity + 5; index++)
        {
            danmaku.Enqueue(index % 2 == 0 ? "Calm" : "Loud", "N", "line " + index);
        }

        danmaku.Hide("Loud");
        danmaku.Enqueue("Calm", "N", "after");

        Assert.Equal(16, danmaku.WaitingCount);
        for (var now = 1_000L; now < 4_000; now += 100)
        {
            danmaku.Release(now, PartyDanmaku.MaximumLanes, MeasureNarrow);
        }

        Assert.Equal(0, danmaku.WaitingCount);
        var started = new List<DanmakuLine>();
        foreach (var line in danmaku.Active)
        {
            if (line.StartedAtTicks != 0)
            {
                started.Add(line);
            }
        }

        started.Sort((left, right) => left.StartedAtTicks != right.StartedAtTicks
            ? left.StartedAtTicks.CompareTo(right.StartedAtTicks)
            : left.Lane.CompareTo(right.Lane));
        var texts = new List<string>();
        foreach (var line in started)
        {
            Assert.Equal("Calm", line.UserId);
            texts.Add(line.Text);
        }

        Assert.Equal(["line 6", "line 8", "line 10", "line 12", "line 14", "line 16", "line 18", "line 20", "line 22",
            "line 24", "line 26", "line 28", "line 30", "line 32", "line 34", "after"], texts);
    }

    [Fact]
    public void ADanmakuLineCrossesTheScreenInItsTravelTimeAndThenIsGone()
    {
        var danmaku = new PartyDanmaku();
        danmaku.Enqueue("User", "N", "hello");
        danmaku.Release(1_000, 3, MeasureByLength);
        var line = FirstStarted(danmaku);

        Assert.True(PartyDanmaku.TryProgress(line, 1_000 + PartyDanmaku.TravelMilliseconds / 2, out var progress));
        Assert.Equal(0.5f, progress, 3);
        Assert.False(PartyDanmaku.TryProgress(line, 1_000 + PartyDanmaku.TravelMilliseconds, out _));

        danmaku.Release(1_000 + PartyDanmaku.TravelMilliseconds, 3, MeasureByLength);
        Assert.Empty(LanesOf(danmaku));
    }

    private static float MeasureByLength(string text) => text.Length * 0.5f;

    private static float MeasureNarrow(string text) => 0.1f;

    private static int[] LanesOf(PartyDanmaku danmaku)
    {
        var lanes = new List<int>();
        foreach (var line in danmaku.Active)
        {
            if (line.StartedAtTicks != 0)
            {
                lanes.Add(line.Lane);
            }
        }

        lanes.Sort();
        return [.. lanes];
    }

    private static DanmakuLine FirstStarted(PartyDanmaku danmaku)
    {
        var first = default(DanmakuLine);
        foreach (var line in danmaku.Active)
        {
            if (line.StartedAtTicks != 0 && (first.StartedAtTicks == 0 || line.StartedAtTicks < first.StartedAtTicks))
            {
                first = line;
            }
        }

        return first;
    }
}
