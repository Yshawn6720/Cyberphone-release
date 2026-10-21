using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal static class GifMedia
{
    public const long MaxBytes = 4L * 1024 * 1024;

    public static bool IsGif(string? pathOrUrl)
    {
        return pathOrUrl is not null && pathOrUrl.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    }

    public static bool FitsSizeCap(string path)
    {
        var info = new FileInfo(path);
        return info.Exists && info.Length > 0 && info.Length <= MaxBytes;
    }

    public static IDalamudTextureWrap? Texture(RemoteImageCache images, string? url, double timeSeconds)
    {
        if (IsGif(url))
        {
            return images.GetAnimated(url)?.FrameAt(timeSeconds);
        }

        return images.Get(url);
    }
}
