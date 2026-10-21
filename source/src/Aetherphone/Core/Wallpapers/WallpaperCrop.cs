namespace Aetherphone.Core.Wallpapers;

internal readonly struct WallpaperCrop
{
    public const float MinZoom = 1f;
    public const float MaxZoom = 5f;
    private const float MinZoomFloor = 0.01f;
    public readonly float Zoom;
    public readonly float CenterX;
    public readonly float CenterY;

    public WallpaperCrop(float zoom, float centerX, float centerY)
    {
        Zoom = zoom;
        CenterX = centerX;
        CenterY = centerY;
    }

    public static WallpaperCrop Cover => new(1f, 0.5f, 0.5f);
    public WallpaperCrop With(float zoom, float centerX, float centerY) => new(zoom, centerX, centerY);

    // The zoom at which the whole image fits inside targetAspect rather than covering it. Always
    // <= MinZoom, equal to it only when the image already matches targetAspect.
    public static float MinZoomToReveal(Vector2 imageSize, float targetAspect)
    {
        if (imageSize.X <= 0f || imageSize.Y <= 0f || targetAspect <= 0f)
        {
            return MinZoom;
        }

        var imageAspect = imageSize.X / imageSize.Y;
        return Math.Min(MinZoom, Math.Min(imageAspect / targetAspect, targetAspect / imageAspect));
    }

    // minZoom defaults to the cover floor so callers that skip Clamped (WallpaperRenderer) keep it.
    public (Vector2 Uv0, Vector2 Uv1) ComputeUv(Vector2 imageSize, float targetAspect, float minZoom = MinZoom)
    {
        var (visibleWidth, visibleHeight) = VisibleSize(imageSize, targetAspect, MathF.Max(minZoom, Zoom));
        var center = new Vector2(ClampCenter(CenterX, visibleWidth), ClampCenter(CenterY, visibleHeight));
        var half = new Vector2(visibleWidth * 0.5f, visibleHeight * 0.5f);
        return (center - half, center + half);
    }

    public WallpaperCrop Clamped(Vector2 imageSize, float targetAspect, float minZoom = MinZoom)
    {
        var zoom = Math.Clamp(Zoom, minZoom, MaxZoom);
        var (visibleWidth, visibleHeight) = VisibleSize(imageSize, targetAspect, zoom);
        return new WallpaperCrop(zoom, ClampCenter(CenterX, visibleWidth), ClampCenter(CenterY, visibleHeight));
    }

    private static (float Width, float Height) VisibleSize(Vector2 imageSize, float targetAspect, float zoom)
    {
        if (imageSize.X <= 0f || imageSize.Y <= 0f || targetAspect <= 0f)
        {
            return (1f, 1f);
        }

        var imageAspect = imageSize.X / imageSize.Y;
        float visibleWidthPixels;
        float visibleHeightPixels;
        if (imageAspect >= targetAspect)
        {
            visibleHeightPixels = imageSize.Y;
            visibleWidthPixels = imageSize.Y * targetAspect;
        }
        else
        {
            visibleWidthPixels = imageSize.X;
            visibleHeightPixels = imageSize.X / targetAspect;
        }

        var boundedZoom = MathF.Max(MinZoomFloor, zoom);
        var width = MathF.Min(1f, visibleWidthPixels / imageSize.X / boundedZoom);
        var height = MathF.Min(1f, visibleHeightPixels / imageSize.Y / boundedZoom);
        return (width, height);
    }

    private static float ClampCenter(float center, float extent)
    {
        var half = extent * 0.5f;
        if (half >= 0.5f)
        {
            return 0.5f;
        }

        return Math.Clamp(center, half, 1f - half);
    }
}
