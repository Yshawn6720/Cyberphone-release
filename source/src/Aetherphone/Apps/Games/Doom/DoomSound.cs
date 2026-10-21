using ManagedDoom;
using ManagedDoom.Audio;
using NAudio.Wave;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomSound : ISound, ISampleProvider
{
    private const int ChannelCount = 8;
    private const int MaxVolumeLevel = 15;
    private const float MasterGain = 0.6f;
    private const float ClipDistance = 1200f;
    private const float CloseDistance = 160f;
    private const float Attenuator = ClipDistance - CloseDistance;
    private const float PauseGraceSeconds = 0.2f;
    private const int LumpHeaderBytes = 8;
    private const int DmxPaddingBytes = 16;
    private static readonly float FastDecay = MathF.Pow(0.5f, 1f / (35f / 5f));
    private static readonly float SlowDecay = MathF.Pow(0.5f, 1f / 35f);

    private sealed class Clip
    {
        public byte[] Samples = Array.Empty<byte>();
        public int SampleRate;
        public float Amplitude;
    }

    private sealed class Voice
    {
        public Clip? Clip;
        public double Position;
        public double Step;
        public float Volume;
        public float Pan;
        public bool Playing;
        public bool Paused;

        public double RemainingSeconds => Clip is null ? 0.0 : (Clip.Samples.Length - Position) / Clip.SampleRate;

        public void Play(Clip clip, float pitch)
        {
            Clip = clip;
            Position = 0.0;
            Step = clip.SampleRate * pitch / DoomAudioOutput.SampleRate;
            Playing = true;
            Paused = false;
        }

        public void Stop()
        {
            Playing = false;
            Paused = false;
        }
    }

    private sealed class ChannelInfo
    {
        public Sfx Reserved;
        public Sfx Playing;
        public float Priority;
        public Mobj? Source;
        public SfxType Type;
        public int Volume;
        public Fixed LastX;
        public Fixed LastY;

        public void Clear()
        {
            Reserved = Sfx.NONE;
            Playing = Sfx.NONE;
            Priority = 0f;
            Source = null;
            Type = 0;
            Volume = 0;
            LastX = Fixed.Zero;
            LastY = Fixed.Zero;
        }
    }

    private readonly Config config;
    private readonly Clip?[] clips;
    private readonly Voice[] voices = new Voice[ChannelCount];
    private readonly ChannelInfo[] infos = new ChannelInfo[ChannelCount];
    private readonly Voice uiVoice = new();
    private readonly DoomRandom? random;
    private readonly object gate = new();
    private Sfx uiReserved = Sfx.NONE;
    private Mobj? listener;
    private float masterVolumeDecay;
    private volatile bool muted;

    public DoomSound(Config config, GameContent content)
    {
        this.config = config;
        config.audio_soundvolume = Math.Clamp(config.audio_soundvolume, 0, MaxVolumeLevel);
        clips = new Clip?[DoomInfo.SfxNames.Length];
        random = config.audio_randompitch ? new DoomRandom() : null;
        for (var index = 0; index < DoomInfo.SfxNames.Length; index++)
        {
            var name = "DS" + DoomInfo.SfxNames[index].ToString().ToUpperInvariant();
            if (content.Wad.GetLumpNumber(name) == -1)
            {
                continue;
            }

            clips[index] = LoadClip(content.Wad.ReadLump(name));
        }

        for (var index = 0; index < ChannelCount; index++)
        {
            voices[index] = new Voice();
            infos[index] = new ChannelInfo();
        }

        masterVolumeDecay = (float)config.audio_soundvolume / MaxVolumeLevel;
    }

    public WaveFormat WaveFormat => DoomAudioOutput.Format;

    public bool Muted
    {
        get => muted;
        set => muted = value;
    }

    public int MaxVolume => MaxVolumeLevel;

    public int Volume
    {
        get => config.audio_soundvolume;
        set
        {
            config.audio_soundvolume = value;
            masterVolumeDecay = (float)config.audio_soundvolume / MaxVolumeLevel;
        }
    }

    private static Clip? LoadClip(byte[] data)
    {
        if (data.Length < LumpHeaderBytes)
        {
            return null;
        }

        var sampleRate = BitConverter.ToUInt16(data, 2);
        var sampleCount = BitConverter.ToInt32(data, 4);
        var offset = LumpHeaderBytes;
        if (ContainsDmxPadding(data, sampleCount))
        {
            offset += DmxPaddingBytes;
            sampleCount -= DmxPaddingBytes * 2;
        }

        if (sampleCount <= 0 || offset + sampleCount > data.Length || sampleRate <= 0)
        {
            return null;
        }

        var samples = new byte[sampleCount];
        Array.Copy(data, offset, samples, 0, sampleCount);
        return new Clip { Samples = samples, SampleRate = sampleRate, Amplitude = Amplitude(samples, sampleRate) };
    }

    private static bool ContainsDmxPadding(byte[] data, int sampleCount)
    {
        if (sampleCount < DmxPaddingBytes * 2 || LumpHeaderBytes + sampleCount > data.Length)
        {
            return false;
        }

        var first = data[LumpHeaderBytes];
        for (var index = 1; index < DmxPaddingBytes; index++)
        {
            if (data[LumpHeaderBytes + index] != first)
            {
                return false;
            }
        }

        var last = data[LumpHeaderBytes + sampleCount - 1];
        for (var index = 1; index < DmxPaddingBytes; index++)
        {
            if (data[LumpHeaderBytes + sampleCount - index - 1] != last)
            {
                return false;
            }
        }

        return true;
    }

    private static float Amplitude(byte[] samples, int sampleRate)
    {
        var max = 0;
        var count = Math.Min(sampleRate / 5, samples.Length);
        for (var index = 0; index < count; index++)
        {
            var amplitude = Math.Abs(samples[index] - 128);
            if (amplitude > max)
            {
                max = amplitude;
            }
        }

        return max / 128f;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        if (muted)
        {
            return count;
        }

        lock (gate)
        {
            for (var index = 0; index < ChannelCount; index++)
            {
                Mix(voices[index], buffer, offset, count);
            }

            Mix(uiVoice, buffer, offset, count);
        }

        for (var index = offset; index < offset + count; index++)
        {
            buffer[index] = Math.Clamp(buffer[index], -1f, 1f);
        }

        return count;
    }

    private static void Mix(Voice voice, float[] buffer, int offset, int count)
    {
        if (!voice.Playing || voice.Paused || voice.Clip is null)
        {
            return;
        }

        var samples = voice.Clip.Samples;
        var angle = (Math.Clamp(voice.Pan, -1f, 1f) + 1f) * 0.25f * MathF.PI;
        var gainLeft = voice.Volume * MasterGain * MathF.Cos(angle);
        var gainRight = voice.Volume * MasterGain * MathF.Sin(angle);
        var frames = count / DoomAudioOutput.Channels;
        for (var frame = 0; frame < frames; frame++)
        {
            var index = (int)voice.Position;
            if (index >= samples.Length)
            {
                voice.Playing = false;
                return;
            }

            var sample = (samples[index] - 128) / 128f;
            var slot = offset + frame * DoomAudioOutput.Channels;
            buffer[slot] += sample * gainLeft;
            buffer[slot + 1] += sample * gainRight;
            voice.Position += voice.Step;
        }
    }

    public void SetListener(Mobj listener)
    {
        this.listener = listener;
    }

    public void Update()
    {
        lock (gate)
        {
            for (var index = 0; index < ChannelCount; index++)
            {
                var info = infos[index];
                var voice = voices[index];
                if (info.Playing != Sfx.NONE)
                {
                    if (voice.Playing)
                    {
                        info.Priority *= info.Type == SfxType.Diffuse ? SlowDecay : FastDecay;
                        SetParameters(voice, info);
                    }
                    else
                    {
                        info.Playing = Sfx.NONE;
                        if (info.Reserved == Sfx.NONE)
                        {
                            info.Source = null;
                        }
                    }
                }

                if (info.Reserved == Sfx.NONE)
                {
                    continue;
                }

                var clip = clips[(int)info.Reserved];
                if (clip is null)
                {
                    info.Reserved = Sfx.NONE;
                    continue;
                }

                SetParameters(voice, info);
                voice.Play(clip, Pitch(info.Type, info.Reserved));
                info.Playing = info.Reserved;
                info.Reserved = Sfx.NONE;
            }

            if (uiReserved != Sfx.NONE)
            {
                var clip = clips[(int)uiReserved];
                if (clip is not null)
                {
                    uiVoice.Pan = 0f;
                    uiVoice.Volume = masterVolumeDecay;
                    uiVoice.Play(clip, 1f);
                }

                uiReserved = Sfx.NONE;
            }
        }
    }

    public void StartSound(Sfx sfx)
    {
        if (clips[(int)sfx] is null)
        {
            return;
        }

        uiReserved = sfx;
    }

    public void StartSound(Mobj mobj, Sfx sfx, SfxType type)
    {
        StartSound(mobj, sfx, type, 100);
    }

    public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume)
    {
        var clip = clips[(int)sfx];
        if (clip is null || listener is null)
        {
            return;
        }

        var x = (mobj.X - listener.X).ToFloat();
        var y = (mobj.Y - listener.Y).ToFloat();
        var distance = MathF.Sqrt(x * x + y * y);
        var priority = type == SfxType.Diffuse ? volume : clip.Amplitude * DistanceDecay(distance) * volume;
        lock (gate)
        {
            for (var index = 0; index < ChannelCount; index++)
            {
                var info = infos[index];
                if (info.Source == mobj && info.Type == type)
                {
                    info.Reserved = sfx;
                    info.Priority = priority;
                    info.Volume = volume;
                    return;
                }
            }

            for (var index = 0; index < ChannelCount; index++)
            {
                var info = infos[index];
                if (info.Reserved == Sfx.NONE && info.Playing == Sfx.NONE)
                {
                    Reserve(info, sfx, priority, mobj, type, volume);
                    return;
                }
            }

            var minimumPriority = float.MaxValue;
            var minimumIndex = -1;
            for (var index = 0; index < ChannelCount; index++)
            {
                if (infos[index].Priority < minimumPriority)
                {
                    minimumPriority = infos[index].Priority;
                    minimumIndex = index;
                }
            }

            if (priority >= minimumPriority && minimumIndex >= 0)
            {
                Reserve(infos[minimumIndex], sfx, priority, mobj, type, volume);
            }
        }
    }

    private static void Reserve(ChannelInfo info, Sfx sfx, float priority, Mobj source, SfxType type, int volume)
    {
        info.Reserved = sfx;
        info.Priority = priority;
        info.Source = source;
        info.Type = type;
        info.Volume = volume;
    }

    public void StopSound(Mobj mobj)
    {
        lock (gate)
        {
            for (var index = 0; index < ChannelCount; index++)
            {
                var info = infos[index];
                if (info.Source != mobj)
                {
                    continue;
                }

                info.LastX = mobj.X;
                info.LastY = mobj.Y;
                info.Source = null;
                info.Volume /= 5;
            }
        }
    }

    public void Reset()
    {
        random?.Clear();
        lock (gate)
        {
            for (var index = 0; index < ChannelCount; index++)
            {
                voices[index].Stop();
                infos[index].Clear();
            }

            uiVoice.Stop();
            uiReserved = Sfx.NONE;
        }

        listener = null;
    }

    public void Pause()
    {
        lock (gate)
        {
            for (var index = 0; index < ChannelCount; index++)
            {
                var voice = voices[index];
                if (voice.Playing && voice.RemainingSeconds > PauseGraceSeconds)
                {
                    voice.Paused = true;
                }
            }
        }
    }

    public void Resume()
    {
        lock (gate)
        {
            for (var index = 0; index < ChannelCount; index++)
            {
                voices[index].Paused = false;
            }
        }
    }

    private void SetParameters(Voice voice, ChannelInfo info)
    {
        if (info.Type == SfxType.Diffuse || listener is null)
        {
            voice.Pan = 0f;
            voice.Volume = 0.01f * masterVolumeDecay * info.Volume;
            return;
        }

        var sourceX = info.Source?.X ?? info.LastX;
        var sourceY = info.Source?.Y ?? info.LastY;
        var x = (sourceX - listener.X).ToFloat();
        var y = (sourceY - listener.Y).ToFloat();
        if (MathF.Abs(x) < 16f && MathF.Abs(y) < 16f)
        {
            voice.Pan = 0f;
            voice.Volume = 0.01f * masterVolumeDecay * info.Volume;
            return;
        }

        var distance = MathF.Sqrt(x * x + y * y);
        var angle = MathF.Atan2(y, x) - (float)listener.Angle.ToRadian();
        voice.Pan = -MathF.Sin(angle);
        voice.Volume = 0.01f * masterVolumeDecay * DistanceDecay(distance) * info.Volume;
    }

    private static float DistanceDecay(float distance) =>
        distance < CloseDistance ? 1f : MathF.Max((ClipDistance - distance) / Attenuator, 0f);

    private float Pitch(SfxType type, Sfx sfx)
    {
        if (random is null || sfx == Sfx.ITEMUP || sfx == Sfx.TINK || sfx == Sfx.RADIO)
        {
            return 1f;
        }

        var spread = type == SfxType.Voice ? 0.075f : 0.025f;
        return 1f + spread * (random.Next() - 128) / 128f;
    }
}
