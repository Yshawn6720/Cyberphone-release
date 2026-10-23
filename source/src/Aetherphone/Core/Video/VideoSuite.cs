using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Video;

internal sealed class VideoSuite : IDisposable
{
    private readonly Configuration configuration;

    internal VideoSuite(PhoneServices services, Configuration configuration, IChatGui chatGui)
    {
        this.configuration = configuration;
        Screen = new ScreenController(() => configuration.VideoHideNameplates);
        Player = new VideoPlayer(Screen.Engine);
        PlayerB = new VideoPlayer(Screen.EngineB);
        Library = new VideoLibrary(configuration);
        Queue = new AetherStreamQueue(Player, services.VideoMetadata, Library, configuration);
        ChannelB = new ChannelBPlayback(PlayerB, Screen.EngineB, Queue, Library);
        WatchAlong = new WatchAlongSession(services.AethernetSession, configuration, services.Confirm, Player, Queue,
            services.StreamSignals, Screen, services.Http, ChannelB);
        Suggestions = new StreamSuggestionNotifier(WatchAlong, services.Notifications);
        ChatFeed = new ScreenChatFeed(chatGui, configuration, Screen);
        ApplySettings();
    }

    internal ScreenController Screen { get; }
    internal VideoPlayer Player { get; }
    internal VideoPlayer PlayerB { get; }
    internal VideoLibrary Library { get; }
    internal AetherStreamQueue Queue { get; }
    internal ChannelBPlayback ChannelB { get; }
    internal WatchAlongSession WatchAlong { get; }
    internal StreamSuggestionNotifier Suggestions { get; }
    internal ScreenChatFeed ChatFeed { get; }

    internal bool PlacingScreen { get; set; }

    internal bool PlacingScreenB { get; set; }

    private void ApplySettings()
    {
        ApplyPlayerSettings();
        ApplyChannelVolumes();
        Screen.Engine.ScreenVisible = configuration.VideoScreenVisible;
        Screen.EngineB.ScreenVisible = configuration.VideoScreenVisible;
        Screen.Engine.ScreenCurve = configuration.VideoScreenCurve;
        Screen.EngineB.ScreenCurve = configuration.VideoScreenCurve;
    }

    internal void ApplyPlayerSettings()
    {
        ApplyPlayerSettings(Player);
        ApplyPlayerSettings(PlayerB);
    }

    private void ApplyPlayerSettings(VideoPlayer player)
    {
        player.HardwareDecoding = configuration.VideoHardwareDecoding;
        player.AllowInsecureDirectUrls = configuration.VideoAllowInsecureDirectUrls;
        player.MaxQualityHeight = configuration.VideoMaxQualityHeight;
        player.SpatialAudio = configuration.VideoSpatialAudio;
        player.SpatialRange = configuration.VideoSpatialRange;
        player.MuteInBackground = configuration.VideoMuteInBackground;
    }

    private void ApplyChannelVolumes()
    {
        var volumes = DualVideo.Volumes((int)(configuration.VideoVolume * 100), configuration.VideoSpatialAudio,
            configuration.VideoMainChannel, Player.HasMedia, PlayerB.HasMedia);
        Player.SetVolume(volumes.A);
        PlayerB.SetVolume(volumes.B);
    }

    internal void OnFrameworkUpdate(float deltaSeconds)
    {
        ApplyChannelVolumes();
        Player.OnFrameworkUpdate();
        PlayerB.OnFrameworkUpdate();
        Queue.OnFrameworkUpdate();
        WatchAlong.OnFrameworkUpdate(deltaSeconds);
        if (PlacingScreen && !Screen.Engine.IsActive)
        {
            PlacingScreen = false;
        }

        if (PlacingScreenB && !Screen.EngineB.IsActive)
        {
            PlacingScreenB = false;
        }
    }

    public void Dispose()
    {
        ChatFeed.Dispose();
        Suggestions.Dispose();
        WatchAlong.Dispose();
        ChannelB.Dispose();
        Queue.Dispose();
        PlayerB.Dispose();
        Player.Dispose();
        Screen.Dispose();
    }
}
