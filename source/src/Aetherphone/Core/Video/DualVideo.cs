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

internal readonly record struct ChannelView(bool ShowA, bool ShowB);

internal readonly record struct ChannelVolumes(int A, int B);

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

    internal static ChannelVolumes Volumes(int volume, bool spatialAudio, VideoChannel mainChannel, bool playingA,
        bool playingB)
    {
        if (spatialAudio || !playingA || !playingB)
        {
            return new ChannelVolumes(volume, volume);
        }

        return mainChannel == VideoChannel.B ? new ChannelVolumes(0, volume) : new ChannelVolumes(volume, 0);
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
