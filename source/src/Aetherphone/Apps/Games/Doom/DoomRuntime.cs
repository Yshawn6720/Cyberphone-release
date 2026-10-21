using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using ManagedDoom;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomRuntime : IDisposable
{
    public const float TicSeconds = 1f / 35f;
    private const float MaxCatchUpSeconds = 0.25f;
    private readonly Config config;
    private readonly GameContent content;
    private readonly DoomVideo video;
    private readonly DoomAudioOutput audio;
    private readonly DoomSound sound;
    private readonly DoomMusic? music;
    private readonly ManagedDoom.Doom doom;
    private float accumulator;
    private bool muted;

    public DoomRuntime(string iwadPath, string? soundfontPath, string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        ConfigUtilities.DataDirectoryOverride = dataDirectory;
        config = new Config(ConfigUtilities.GetConfigPath());
        config.video_highresolution = false;
        config.video_fpsscale = 1;
        config.game_alwaysrun = true;
        var args = new CommandLineArgs(new[] { "-iwad", iwadPath });
        content = new GameContent(args);
        video = new DoomVideo(config, content);
        audio = new DoomAudioOutput();
        sound = new DoomSound(config, content);
        audio.Add(sound);
        if (soundfontPath is not null)
        {
            try
            {
                music = new DoomMusic(config, content, soundfontPath);
                audio.Add(music);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Doom] Music could not start; playing without it.");
                music = null;
            }
        }

        Input = new DoomInput(config);
        doom = new ManagedDoom.Doom(args, config, content, video, sound, music, Input);
        audio.Start();
    }

    public DoomInput Input { get; }
    public ManagedDoom.Doom Doom => doom;
    public bool Finished { get; private set; }
    public bool InMenu => doom.Menu.Active;
    public float AspectRatio => 4f / 3f;

    public bool Muted
    {
        get => muted;
        set
        {
            if (muted == value)
            {
                return;
            }

            muted = value;
            sound.Muted = value;
            if (music is not null)
            {
                music.Muted = value;
            }

            if (value)
            {
                sound.Pause();
            }
            else
            {
                sound.Resume();
            }
        }
    }

    public void Tick(float deltaSeconds, bool keyboardActive)
    {
        if (Finished)
        {
            return;
        }

        Input.PumpEvents(doom, keyboardActive);
        accumulator = MathF.Min(accumulator + deltaSeconds, MaxCatchUpSeconds);
        while (accumulator >= TicSeconds)
        {
            accumulator -= TicSeconds;
            if (doom.Update() == UpdateResult.Completed)
            {
                Finished = true;
                return;
            }
        }
    }

    public void Render()
    {
        var fraction = Math.Clamp(accumulator / TicSeconds, 0f, 1f);
        video.Render(doom, Fixed.FromDouble(fraction));
    }

    public void Present(ImDrawListPtr drawList, Rect screen)
    {
        video.Present(drawList, screen);
    }

    public void Dispose()
    {
        try
        {
            config.Save(ConfigUtilities.GetConfigPath());
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Doom] Could not save the engine settings: {exception.Message}");
        }

        audio.Dispose();
        video.Dispose();
        content.Dispose();
    }
}
