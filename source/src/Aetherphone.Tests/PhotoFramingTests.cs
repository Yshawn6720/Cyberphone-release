using System.Numerics;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhotoFramingTests
{
    private static readonly Vector2 Landscape16By9 = new(1920f, 1080f);

    [Fact]
    public void MinZoomToRevealFitsLandscapeSourceInsidePortraitBox()
    {
        var minZoom = WallpaperCrop.MinZoomToReveal(Landscape16By9, PostAspects.PortraitRatio);

        Assert.True(minZoom < WallpaperCrop.MinZoom);
        var crop = new WallpaperCrop(minZoom, 0.5f, 0.5f);
        var (uv0, uv1) = crop.ComputeUv(Landscape16By9, PostAspects.PortraitRatio, minZoom);
        Assert.Equal(0f, uv0.X, 3);
        Assert.Equal(0f, uv0.Y, 3);
        Assert.Equal(1f, uv1.X, 3);
        Assert.Equal(1f, uv1.Y, 3);
    }

    [Fact]
    public void MinZoomToRevealIsCoverWhenAspectAlreadyMatches()
    {
        var minZoom = WallpaperCrop.MinZoomToReveal(new Vector2(1080f, 1080f), PostAspects.SquareRatio);

        Assert.Equal(WallpaperCrop.MinZoom, minZoom, 4);
    }

    [Fact]
    public void MinZoomToRevealFallsBackToCoverOnDegenerateInput()
    {
        Assert.Equal(WallpaperCrop.MinZoom, WallpaperCrop.MinZoomToReveal(Vector2.Zero, PostAspects.PortraitRatio), 4);
        Assert.Equal(WallpaperCrop.MinZoom, WallpaperCrop.MinZoomToReveal(Landscape16By9, 0f), 4);
    }

    [Fact]
    public void ComputeUvKeepsTheCoverFloorWhenNoMinZoomIsPassed()
    {
        var crop = new WallpaperCrop(0.25f, 0.5f, 0.5f);

        var (uv0, uv1) = crop.ComputeUv(Landscape16By9, PostAspects.PortraitRatio);

        Assert.True(uv1.X - uv0.X < 1f);
        Assert.Equal(1f, uv1.Y - uv0.Y, 3);
    }

    [Fact]
    public void ContainSizeShrinksToFitWithoutChangingAspect()
    {
        var (width, height) = ImageProcessor.ContainSize(1920, 1080, 810, 1080);

        Assert.Equal(810, width);
        Assert.Equal(456, height);
    }

    [Fact]
    public void ContainSizeIsExactWhenTheRegionAlreadyMatchesTheTarget()
    {
        var (width, height) = ImageProcessor.ContainSize(2160, 2160, 1080, 1080);

        Assert.Equal(1080, width);
        Assert.Equal(1080, height);
    }

    [Fact]
    public void ContainSizeFallsBackToTheTargetOnDegenerateInput()
    {
        var (width, height) = ImageProcessor.ContainSize(0, 0, 1080, 1350);

        Assert.Equal(1080, width);
        Assert.Equal(1350, height);
    }
}
