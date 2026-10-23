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

internal readonly record struct ChannelView(bool ShowA, bool ShowB);

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
        if (!liveA || !liveB)
        {
            return new ChannelVolumes(sound.Volume, sound.Volume);
        }

        if (sound.BothScreens)
        {
            return new ChannelVolumes(sound.MutedA ? 0 : sound.Volume, sound.MutedB ? 0 : sound.VolumeB);
        }

        return sound.MainChannel == VideoChannel.B
            ? new ChannelVolumes(0, sound.Volume)
            : new ChannelVolumes(sound.Volume, 0);
    }

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
