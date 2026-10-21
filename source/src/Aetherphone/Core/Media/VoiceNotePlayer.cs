using Aetherphone.Core.Audio;
using Aetherphone.Core.Playback;
using NAudio.Wave;

namespace Aetherphone.Core.Media;

internal enum VoiceNoteFailure : byte
{
    None = 0,
    NoKey = 1,
    Unavailable = 2,
}

internal readonly record struct VoiceNoteState(bool Current, bool Playing, float Progress, VoiceNoteFailure Failure);

internal sealed class VoiceNotePlayer : IDisposable
{
    private IWavePlayer? output;
    private WaveFileReader? reader;
    private string? playingId;

    public VoiceNoteState StateFor(string messageId)
    {
        if (playingId != messageId || output is null || reader is null)
        {
            return default;
        }

        var total = reader.TotalTime.TotalSeconds;
        var progress = total <= 0 ? 0f : (float)Math.Clamp(reader.CurrentTime.TotalSeconds / total, 0d, 1d);
        return new VoiceNoteState(true, output.PlaybackState == PlaybackState.Playing, progress, VoiceNoteFailure.None);
    }

    public void Toggle(string messageId, byte[] wavBytes)
    {
        if (playingId == messageId && output is not null && reader is not null)
        {
            if (output.PlaybackState == PlaybackState.Playing)
            {
                output.Pause();
                return;
            }

            if (reader.Position >= reader.Length)
            {
                reader.Position = 0;
            }

            try
            {
                output.Play();
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Voice note playback failed");
                Stop();
            }

            return;
        }

        Stop();
        try
        {
            reader = new WaveFileReader(new MemoryStream(wavBytes, writable: false));
            output = AudioOutputFactory.Create();
            output.Init(reader);
            output.Play();
            playingId = messageId;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Voice note playback failed");
            Stop();
        }
    }

    public void Stop()
    {
        try
        {
            output?.Stop();
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "Stopping voice note playback failed");
        }

        output?.Dispose();
        output = null;
        reader?.Dispose();
        reader = null;
        playingId = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
