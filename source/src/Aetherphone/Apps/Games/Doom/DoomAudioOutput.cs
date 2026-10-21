using Aetherphone.Core;
using Aetherphone.Core.Audio;
using Aetherphone.Core.Playback;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomAudioOutput : IDisposable
{
    public const int SampleRate = 44100;
    public const int Channels = 2;
    private const int LatencyMilliseconds = 120;
    private readonly MixingSampleProvider mixer =
        new(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels)) { ReadFully = true };
    private IWavePlayer? player;

    public static WaveFormat Format => WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

    public void Add(ISampleProvider provider)
    {
        mixer.AddMixerInput(provider);
    }

    public void Start()
    {
        if (player is not null)
        {
            return;
        }

        try
        {
            player = AudioOutputFactory.Create(LatencyMilliseconds);
            player.Init(mixer);
            player.Play();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Doom] Audio output could not start; playing silent.");
            player?.Dispose();
            player = null;
        }
    }

    public void Dispose()
    {
        if (player is null)
        {
            return;
        }

        try
        {
            player.Stop();
        }
        catch (Exception exception)
        {
            AepLog.Debug($"[Doom] Audio output stop failed: {exception.Message}");
        }

        player.Dispose();
        player = null;
    }
}
