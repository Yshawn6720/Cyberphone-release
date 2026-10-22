using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VideoFrameLayoutTests
{
    [Theory]
    [InlineData(2560f, 1080f, 16f / 9f, 320f, 0f, 1920f, 1080f, 0f, 0f, 1f, 1f)]
    [InlineData(1920f, 1080f, 4f / 3f, 240f, 0f, 1440f, 1080f, 0.125f, 0f, 0.875f, 1f)]
    [InlineData(1920f, 1080f, 9f / 16f, 656.25f, 0f, 607.5f, 1080f, 0.341796875f, 0f, 0.658203125f, 1f)]
    [InlineData(1920f, 1080f, 7f / 3f, 0f, 128.57143f, 1920f, 822.85714f, 0f, 0.11904762f, 1f, 0.88095238f)]
    public void FitShowsWholeSourceWithoutEmbeddedTextureBars(float width, float height, float aspect,
        float left, float top, float frameWidth, float frameHeight, float uMin, float vMin, float uMax, float vMax)
    {
        var area = new Rect(new Vector2(100f, 50f), new Vector2(100f + width, 50f + height));
        var result = VideoFrameLayout.Calculate(area, new Vector2(1920f, 1080f), aspect, VideoFitMode.Fit);
        AssertNear(100f + left, result.Frame.Min.X);
        AssertNear(50f + top, result.Frame.Min.Y);
        AssertNear(frameWidth, result.Frame.Width);
        AssertNear(frameHeight, result.Frame.Height);
        AssertUv(result, uMin, vMin, uMax, vMax);
    }

    [Theory]
    [InlineData(2560f, 1080f, 16f / 9f, 0f, 0.125f, 1f, 0.875f)]
    [InlineData(1920f, 1080f, 4f / 3f, 0.125f, 0.125f, 0.875f, 0.875f)]
    [InlineData(1080f, 1920f, 16f / 9f, 0.341796875f, 0f, 0.658203125f, 1f)]
    [InlineData(1920f, 1080f, 7f / 3f, 0.11904762f, 0.11904762f, 0.88095238f, 0.88095238f)]
    public void FillCoversViewportWithCenteredCropWithoutStretch(float width, float height, float aspect,
        float uMin, float vMin, float uMax, float vMax)
    {
        var area = new Rect(new Vector2(50f, 100f), new Vector2(50f + width, 100f + height));
        var result = VideoFrameLayout.Calculate(area, new Vector2(1920f, 1080f), aspect, VideoFitMode.Fill);
        Assert.Equal(area, result.Frame);
        AssertUv(result, uMin, vMin, uMax, vMax);
        var visibleAspect = (result.UvMax.X - result.UvMin.X) * 1920f
            / ((result.UvMax.Y - result.UvMin.Y) * 1080f);
        AssertNear(width / height, visibleAspect);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void MissingAspectDuringLoadingUsesTextureAspect(float aspect)
    {
        var area = new Rect(Vector2.Zero, new Vector2(1920f, 1080f));
        var result = VideoFrameLayout.Calculate(area, new Vector2(1920f, 1080f), aspect, VideoFitMode.Fit);
        Assert.Equal(area, result.Frame);
        AssertUv(result, 0f, 0f, 1f, 1f);
    }

    private static void AssertUv(VideoFrameLayout layout, float uMin, float vMin, float uMax, float vMax)
    {
        AssertNear(uMin, layout.UvMin.X);
        AssertNear(vMin, layout.UvMin.Y);
        AssertNear(uMax, layout.UvMax.X);
        AssertNear(vMax, layout.UvMax.Y);
    }

    private static void AssertNear(float expected, float actual) => Assert.Equal(expected, actual, 0.001f);
}
