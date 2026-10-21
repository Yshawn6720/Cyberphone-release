using ManagedDoom;
using ManagedDoom.Audio;
using MeltySynth;
using NAudio.Wave;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomMusic : IMusic, ISampleProvider
{
    private const int MaxVolumeLevel = 15;
    private const float MasterGain = 0.5f;
    private const int MidiTicksPerSecond = 140;
    private const int BlockLength = DoomAudioOutput.SampleRate / MidiTicksPerSecond;
    private static readonly byte[] MusHeader = { (byte)'M', (byte)'U', (byte)'S', 0x1A };
    private static readonly byte[] MidiHeader = { (byte)'M', (byte)'T', (byte)'h', (byte)'d' };

    private interface IDecoder
    {
        void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right);
    }

    private readonly Config config;
    private readonly Wad wad;
    private readonly Synthesizer synthesizer;
    private readonly float[] left = new float[BlockLength];
    private readonly float[] right = new float[BlockLength];
    private readonly object gate = new();
    private IDecoder? current;
    private IDecoder? reserved;
    private Bgm currentBgm = Bgm.NONE;
    private volatile bool muted;

    public DoomMusic(Config config, GameContent content, string soundfontPath)
    {
        this.config = config;
        wad = content.Wad;
        config.audio_musicvolume = Math.Clamp(config.audio_musicvolume, 0, MaxVolumeLevel);
        var settings = new SynthesizerSettings(DoomAudioOutput.SampleRate)
        {
            BlockSize = BlockLength,
            EnableReverbAndChorus = config.audio_musiceffect,
        };
        synthesizer = new Synthesizer(soundfontPath, settings);
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
        get => config.audio_musicvolume;
        set => config.audio_musicvolume = value;
    }

    public void StartMusic(Bgm bgm, bool loop)
    {
        if (bgm == currentBgm)
        {
            return;
        }

        var lump = "D_" + DoomInfo.BgmNames[(int)bgm].ToString().ToUpperInvariant();
        var data = wad.ReadLump(lump);
        var decoder = CreateDecoder(data, loop);
        lock (gate)
        {
            reserved = decoder;
        }

        currentBgm = bgm;
    }

    private static IDecoder CreateDecoder(byte[] data, bool loop)
    {
        if (StartsWith(data, MusHeader))
        {
            return new MusDecoder(data, loop);
        }

        if (StartsWith(data, MidiHeader))
        {
            return new MidiDecoder(data, loop);
        }

        throw new InvalidOperationException("The music lump is neither MUS nor MIDI.");
    }

    private static bool StartsWith(byte[] data, byte[] header)
    {
        if (data.Length < header.Length)
        {
            return false;
        }

        for (var index = 0; index < header.Length; index++)
        {
            if (data[index] != header[index])
            {
                return false;
            }
        }

        return true;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (gate)
        {
            if (reserved != current)
            {
                synthesizer.Reset();
                current = reserved;
            }

            if (current is null || muted)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            var gain = MasterGain * 2f * config.audio_musicvolume / MaxVolumeLevel;
            var frames = count / DoomAudioOutput.Channels;
            var written = 0;
            while (written < frames)
            {
                var chunk = Math.Min(BlockLength, frames - written);
                current.RenderWaveform(synthesizer, left.AsSpan(0, chunk), right.AsSpan(0, chunk));
                for (var frame = 0; frame < chunk; frame++)
                {
                    var slot = offset + (written + frame) * DoomAudioOutput.Channels;
                    buffer[slot] = Math.Clamp(left[frame] * gain, -1f, 1f);
                    buffer[slot + 1] = Math.Clamp(right[frame] * gain, -1f, 1f);
                }

                written += chunk;
            }
        }

        return count;
    }

    private sealed class MusDecoder : IDecoder
    {
        private const int MusChannels = 16;
        private const int MaxEventsPerGroup = 128;
        private const int PercussionMusChannel = 15;
        private const int PercussionMidiChannel = 9;

        private struct MusEvent
        {
            public int Type;
            public int Channel;
            public int Data1;
            public int Data2;
        }

        private enum ReadResult : byte
        {
            Ongoing,
            EndOfGroup,
            EndOfFile,
        }

        private readonly byte[] data;
        private readonly bool loop;
        private readonly int scoreStart;
        private readonly MusEvent[] events = new MusEvent[MaxEventsPerGroup];
        private readonly int[] lastVolume = new int[MusChannels];
        private int eventCount;
        private int position;
        private int delay;
        private int blockWritten;

        public MusDecoder(byte[] data, bool loop)
        {
            this.data = data;
            this.loop = loop;
            scoreStart = BitConverter.ToUInt16(data, 6);
            Reset();
            blockWritten = BlockLength;
        }

        public void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right)
        {
            var written = 0;
            while (written < left.Length)
            {
                if (blockWritten == synthesizer.BlockSize)
                {
                    ProcessMidiEvents(synthesizer);
                    blockWritten = 0;
                }

                var sourceRemaining = synthesizer.BlockSize - blockWritten;
                var destinationRemaining = left.Length - written;
                var remaining = Math.Min(sourceRemaining, destinationRemaining);
                synthesizer.Render(left.Slice(written, remaining), right.Slice(written, remaining));
                blockWritten += remaining;
                written += remaining;
            }
        }

        private void ProcessMidiEvents(Synthesizer synthesizer)
        {
            if (delay > 0)
            {
                delay--;
            }

            if (delay != 0)
            {
                return;
            }

            delay = ReadSingleEventGroup();
            SendEvents(synthesizer);
            if (delay != -1)
            {
                return;
            }

            synthesizer.NoteOffAll(false);
            if (loop)
            {
                Reset();
            }
        }

        private void Reset()
        {
            Array.Clear(lastVolume);
            position = scoreStart;
            delay = 0;
        }

        private int ReadSingleEventGroup()
        {
            eventCount = 0;
            while (true)
            {
                var result = ReadSingleEvent();
                if (result == ReadResult.EndOfGroup)
                {
                    break;
                }

                if (result == ReadResult.EndOfFile)
                {
                    return -1;
                }
            }

            var time = 0;
            while (true)
            {
                var value = data[position++];
                time = time * 128 + (value & 127);
                if ((value & 128) == 0)
                {
                    break;
                }
            }

            return time;
        }

        private ReadResult ReadSingleEvent()
        {
            var channel = data[position] & 0xF;
            if (channel == PercussionMusChannel)
            {
                channel = PercussionMidiChannel;
            }
            else if (channel >= PercussionMidiChannel)
            {
                channel++;
            }

            var eventType = (data[position] & 0x70) >> 4;
            var last = (data[position] >> 7) != 0;
            position++;
            if (eventType == 6)
            {
                return ReadResult.EndOfFile;
            }

            if (eventCount >= MaxEventsPerGroup)
            {
                return ReadResult.EndOfFile;
            }

            ref var musEvent = ref events[eventCount++];
            musEvent.Type = eventType;
            musEvent.Channel = channel;
            switch (eventType)
            {
                case 0:
                    musEvent.Data1 = data[position++];
                    musEvent.Data2 = 0;
                    break;
                case 1:
                    var playNote = data[position++];
                    musEvent.Data1 = playNote & 127;
                    if ((playNote & 128) != 0)
                    {
                        var noteVolume = data[position++];
                        musEvent.Data2 = noteVolume;
                        lastVolume[channel] = noteVolume;
                    }
                    else
                    {
                        musEvent.Data2 = lastVolume[channel];
                    }

                    break;
                case 2:
                    var pitchWheel = data[position++];
                    var wheel = (pitchWheel << 7) / 2;
                    musEvent.Data1 = wheel & 127;
                    musEvent.Data2 = wheel >> 7;
                    break;
                case 3:
                    musEvent.Data1 = data[position++];
                    musEvent.Data2 = 0;
                    break;
                case 4:
                    musEvent.Data1 = data[position++];
                    musEvent.Data2 = data[position++];
                    break;
                default:
                    throw new InvalidOperationException("Unknown MUS event type.");
            }

            return last ? ReadResult.EndOfGroup : ReadResult.Ongoing;
        }

        private void SendEvents(Synthesizer synthesizer)
        {
            for (var index = 0; index < eventCount; index++)
            {
                var musEvent = events[index];
                switch (musEvent.Type)
                {
                    case 0:
                        synthesizer.NoteOff(musEvent.Channel, musEvent.Data1);
                        break;
                    case 1:
                        synthesizer.NoteOn(musEvent.Channel, musEvent.Data1, musEvent.Data2);
                        break;
                    case 2:
                        synthesizer.ProcessMidiMessage(musEvent.Channel, 0xE0, musEvent.Data1, musEvent.Data2);
                        break;
                    case 3:
                        SendSystemEvent(synthesizer, musEvent);
                        break;
                    case 4:
                        SendControlChange(synthesizer, musEvent);
                        break;
                }
            }
        }

        private static void SendSystemEvent(Synthesizer synthesizer, in MusEvent musEvent)
        {
            switch (musEvent.Data1)
            {
                case 11:
                    synthesizer.NoteOffAll(musEvent.Channel, false);
                    break;
                case 14:
                    synthesizer.ResetAllControllers(musEvent.Channel);
                    break;
            }
        }

        private static void SendControlChange(Synthesizer synthesizer, in MusEvent musEvent)
        {
            switch (musEvent.Data1)
            {
                case 0:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xC0, musEvent.Data2, 0);
                    break;
                case 1:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xB0, 0x00, musEvent.Data2);
                    break;
                case 2:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xB0, 0x01, musEvent.Data2);
                    break;
                case 3:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xB0, 0x07, musEvent.Data2);
                    break;
                case 4:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xB0, 0x0A, musEvent.Data2);
                    break;
                case 5:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xB0, 0x0B, musEvent.Data2);
                    break;
                case 6:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xB0, 0x5B, musEvent.Data2);
                    break;
                case 7:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xB0, 0x5D, musEvent.Data2);
                    break;
                case 8:
                    synthesizer.ProcessMidiMessage(musEvent.Channel, 0xB0, 0x40, musEvent.Data2);
                    break;
            }
        }
    }

    private sealed class MidiDecoder : IDecoder
    {
        private readonly MidiFile midi;
        private readonly bool loop;
        private MidiFileSequencer? sequencer;

        public MidiDecoder(byte[] data, bool loop)
        {
            midi = new MidiFile(new MemoryStream(data));
            this.loop = loop;
        }

        public void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right)
        {
            if (sequencer is null)
            {
                sequencer = new MidiFileSequencer(synthesizer);
                sequencer.Play(midi, loop);
            }

            sequencer.Render(left, right);
        }
    }
}
