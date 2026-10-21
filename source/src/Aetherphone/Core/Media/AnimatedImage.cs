using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal sealed class AnimatedImage : IDisposable
{
    public readonly IDalamudTextureWrap[] Frames;
    public readonly float[] DelaysSeconds;
    public readonly float TotalSeconds;
    public readonly long Bytes;

    public AnimatedImage(IDalamudTextureWrap[] frames, float[] delaysSeconds)
    {
        Frames = frames;
        DelaysSeconds = delaysSeconds;
        var total = 0f;
        for (var index = 0; index < delaysSeconds.Length; index++)
        {
            total += delaysSeconds[index];
        }

        TotalSeconds = total;
        long bytes = 0;
        for (var index = 0; index < frames.Length; index++)
        {
            bytes += (long)frames[index].Width * frames[index].Height * 4;
        }

        Bytes = bytes;
    }

    public IDalamudTextureWrap FrameAt(double timeSeconds)
    {
        return Frames[GifFramePlan.FrameIndexAt(DelaysSeconds, TotalSeconds, timeSeconds)];
    }

    public void Dispose()
    {
        for (var index = 0; index < Frames.Length; index++)
        {
            Frames[index].Dispose();
        }
    }
}
