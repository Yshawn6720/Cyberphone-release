using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

internal enum VideoChannel : byte
{
    A,
    B,
}

internal enum VideoPlayMode : byte
{
    Both,
    A,
    B,
}

internal enum VideoWatchChoice : byte
{
    FollowHost,
    A,
    B,
    Both,
}

internal enum ScreenSoundChoice : byte
{
    A,
    B,
    Both,
}

internal enum ScreenBLink : byte
{
    Empty,
    Playable,
    NotALink,
    Playlist,
}

internal enum ScreenStatus : byte
{
    Idle,
    Loading,
    Playing,
    Paused,
    Failed,
}

internal readonly record struct ChannelView(bool ShowA, bool ShowB);

internal readonly record struct ScreenTabs(bool Shown, VideoChannel Screen);

internal readonly record struct ChannelVolumes(int A, int B);

internal readonly record struct ScreenSound(VideoChannel MainChannel, bool BothScreens, int Volume, int VolumeB,
    bool MutedA, bool MutedB);

internal static class DualVideo
{
    internal const string WireA = "a";
    internal const string WireB = "b";
    internal const string WireBoth = "both";

    private const int RequiredFeatures = StreamFeature.Party | StreamFeature.DualVideo;

    internal static bool Supported(int serverFeatures) => (serverFeatures & RequiredFeatures) == RequiredFeatures;

    internal static string ChannelWire(VideoChannel channel) => channel == VideoChannel.B ? WireB : WireA;

    internal static VideoChannel ParseChannel(string? wire) => wire == WireB ? VideoChannel.B : VideoChannel.A;

    internal static string PlayModeWire(VideoPlayMode mode) => mode switch
    {
        VideoPlayMode.A => WireA,
        VideoPlayMode.B => WireB,
        _ => WireBoth,
    };

    internal static VideoPlayMode? ParsePlayMode(string? wire) => wire switch
    {
        WireA => VideoPlayMode.A,
        WireB => VideoPlayMode.B,
        WireBoth => VideoPlayMode.Both,
        _ => null,
    };

    internal static ChannelView View(VideoWatchChoice choice, VideoPlayMode? hostMode, bool hasA, bool hasB)
    {
        var mode = choice switch
        {
            VideoWatchChoice.A => VideoPlayMode.A,
            VideoWatchChoice.B => VideoPlayMode.B,
            VideoWatchChoice.Both => VideoPlayMode.Both,
            _ => hostMode ?? VideoPlayMode.Both,
        };

        var showA = hasA && mode != VideoPlayMode.B;
        var showB = hasB && mode != VideoPlayMode.A;
        if (!showA && !showB)
        {
            return new ChannelView(hasA, !hasA && hasB);
        }

        return new ChannelView(showA, showB);
    }

    internal static ChannelView LocalView(bool playingA, bool playingB) => new(playingA || !playingB, playingB);

    internal static ChannelVolumes Volumes(in ScreenSound sound, bool liveA, bool liveB)
    {
        var volumeA = sound.MutedA ? 0 : sound.Volume;
        var volumeB = sound.MutedB ? 0 : sound.VolumeB;
        if (!liveA || !liveB || sound.BothScreens)
        {
            return new ChannelVolumes(volumeA, volumeB);
        }

        return sound.MainChannel == VideoChannel.B
            ? new ChannelVolumes(0, volumeB)
            : new ChannelVolumes(volumeA, 0);
    }

    internal static ScreenSound TapSpeaker(in ScreenSound sound, VideoChannel channel, bool bothLive)
    {
        var isB = channel == VideoChannel.B;
        var muted = isB ? sound.MutedB : sound.MutedA;
        var heard = !muted && (!bothLive || sound.BothScreens || sound.MainChannel == channel);
        if (heard)
        {
            return isB ? sound with { MutedB = true } : sound with { MutedA = true };
        }

        var unmuted = isB ? sound with { MutedB = false } : sound with { MutedA = false };
        return bothLive && !sound.BothScreens ? unmuted with { MainChannel = channel } : unmuted;
    }

    internal static ScreenTabs Tabs(VideoChannel picked, bool wasShown, bool viewing, bool hasA, bool hasB)
    {
        if (!hasB && (viewing || !hasA))
        {
            return new ScreenTabs(false, VideoChannel.A);
        }

        if (!wasShown || (viewing && !hasA))
        {
            return new ScreenTabs(true, hasA ? VideoChannel.A : VideoChannel.B);
        }

        return new ScreenTabs(true, picked);
    }

    internal static ScreenStatus Status(VideoPlaybackState state, bool paused, bool recoveryExhausted) => state switch
    {
        VideoPlaybackState.Loading => ScreenStatus.Loading,
        VideoPlaybackState.Failed => ScreenStatus.Failed,
        VideoPlaybackState.Playing or VideoPlaybackState.Paused when recoveryExhausted => ScreenStatus.Failed,
        VideoPlaybackState.Paused => ScreenStatus.Paused,
        VideoPlaybackState.Playing => paused ? ScreenStatus.Paused : ScreenStatus.Playing,
        _ => ScreenStatus.Idle,
    };

    internal static ScreenSoundChoice SoundChoice(in ScreenSound sound)
    {
        if (sound.BothScreens)
        {
            return ScreenSoundChoice.Both;
        }

        return sound.MainChannel == VideoChannel.B ? ScreenSoundChoice.B : ScreenSoundChoice.A;
    }

    internal static ScreenSound Pick(in ScreenSound sound, ScreenSoundChoice choice) => choice switch
    {
        ScreenSoundChoice.Both => sound with { BothScreens = true },
        ScreenSoundChoice.B => sound with { BothScreens = false, MainChannel = VideoChannel.B },
        _ => sound with { BothScreens = false, MainChannel = VideoChannel.A },
    };

    internal static ScreenBLink CheckScreenBLink(string input)
    {
        if (input.Length == 0)
        {
            return ScreenBLink.Empty;
        }

        if (MediaInput.LooksLikeLocalPath(input))
        {
            return ScreenBLink.Playable;
        }

        if (!VideoEngine.ValidateURL(input, out _))
        {
            return ScreenBLink.NotALink;
        }

        return VideoUrlResolver.IsPlaylistUrl(input) && !VideoUrlResolver.NamesOneVideo(input)
            ? ScreenBLink.Playlist
            : ScreenBLink.Playable;
    }

    internal static VideoChannel WatchedScreen(ChannelView view, VideoChannel mainChannel)
    {
        if (view.ShowA && view.ShowB)
        {
            return mainChannel;
        }

        return view.ShowB ? VideoChannel.B : VideoChannel.A;
    }

    internal static VideoChannel DanmakuTarget(ChannelView view, VideoChannel mainChannel, VideoChannel? picked)
    {
        if (picked == VideoChannel.B && view.ShowB)
        {
            return VideoChannel.B;
        }

        if (picked == VideoChannel.A && view.ShowA)
        {
            return VideoChannel.A;
        }

        return WatchedScreen(view, mainChannel);
    }

    internal static VideoChannel DanmakuScreen(string? lineScreen, ChannelView view)
    {
        if (lineScreen == WireB && view.ShowB)
        {
            return VideoChannel.B;
        }

        if (lineScreen == WireA && view.ShowA)
        {
            return VideoChannel.A;
        }

        return view.ShowB && !view.ShowA ? VideoChannel.B : VideoChannel.A;
    }
}
