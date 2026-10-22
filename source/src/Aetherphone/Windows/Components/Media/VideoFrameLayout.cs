using Aetherphone.Core;

namespace Aetherphone.Windows.Components;

internal enum VideoFitMode : byte
{
    Fit,
    Fill,
}

internal readonly record struct VideoFrameLayout(Rect Frame, Vector2 UvMin, Vector2 UvMax)
{
    internal static VideoFrameLayout Calculate(Rect area, Vector2 textureSize, float sourceAspect, VideoFitMode mode)
    {
        var textureAspect = textureSize.X / textureSize.Y;
        var aspect = float.IsFinite(sourceAspect) && sourceAspect > 0f ? sourceAspect : textureAspect;
        var (sourceMin, sourceMax) = ImageFit.Cover(textureSize.X, textureSize.Y, aspect, 1f);
        if (mode == VideoFitMode.Fit)
        {
            return new VideoFrameLayout(ImageFit.CenteredRect(area, aspect), sourceMin, sourceMax);
        }

        var (cropMin, cropMax) = ImageFit.Cover(aspect, 1f, area.Width, area.Height);
        var sourceSpan = sourceMax - sourceMin;
        return new VideoFrameLayout(area, sourceMin + cropMin * sourceSpan, sourceMin + cropMax * sourceSpan);
    }
}
