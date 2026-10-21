using Aetherphone.Core.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AnimatedImageDecodeTests
{
    private const int Side = 8;
    private const int DelayMilliseconds = 100;
    private static readonly Rgba32[] Colors =
    {
        new(255, 0, 0, 255),
        new(0, 0, 255, 255),
        new(0, 255, 0, 255),
    };

    [Fact]
    public void AStaticPngIsNotAnimated()
    {
        Assert.Equal(AnimationKind.None, ImageProcessor.AnimationKindOf(Encode(1, webp: false)));
    }

    [Fact]
    public void AStaticWebpIsNotAnimated()
    {
        Assert.Equal(AnimationKind.None, ImageProcessor.AnimationKindOf(Encode(1, webp: true)));
    }

    [Fact]
    public void AnApngIsSniffedFromItsControlChunk()
    {
        Assert.Equal(AnimationKind.Png, ImageProcessor.AnimationKindOf(Encode(2, webp: false)));
    }

    [Fact]
    public void AnAnimatedWebpIsSniffedFromItsHeaderFlag()
    {
        Assert.Equal(AnimationKind.Webp, ImageProcessor.AnimationKindOf(Encode(2, webp: true)));
    }

    [Fact]
    public void AGifIsSniffedFromItsSignature()
    {
        var header = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0 };

        Assert.Equal(AnimationKind.Gif, ImageProcessor.AnimationKindOf(header));
    }

    [Fact]
    public void AnApngDecodesEveryFrameWithItsDelay()
    {
        var (frames, width, height, delays) =
            ImageProcessor.DecodeAnimationFrames(Encode(3, webp: false), AnimationKind.Png, 0);

        AssertThreeFrames(frames, width, height, delays);
    }

    [Fact]
    public void AnAnimatedWebpDecodesEveryFrameWithItsDelay()
    {
        var (frames, width, height, delays) =
            ImageProcessor.DecodeAnimationFrames(Encode(3, webp: true), AnimationKind.Webp, 0);

        AssertThreeFrames(frames, width, height, delays);
    }

    [Fact]
    public void AnimationsScaleDownToTheRequestedDimension()
    {
        var (frames, width, height, _) =
            ImageProcessor.DecodeAnimationFrames(Encode(2, webp: false, side: 64), AnimationKind.Png, 32);

        Assert.Equal(2, frames.Length);
        Assert.Equal(32, width);
        Assert.Equal(32, height);
        Assert.Equal(32 * 32 * 4, frames[0].Length);
    }

    private static void AssertThreeFrames(byte[][] frames, int width, int height, float[] delays)
    {
        Assert.Equal(3, frames.Length);
        Assert.Equal(Side, width);
        Assert.Equal(Side, height);
        for (var frameIndex = 0; frameIndex < frames.Length; frameIndex++)
        {
            Assert.Equal(DelayMilliseconds / 1000f, delays[frameIndex], 3);
            var expected = Colors[frameIndex];
            Assert.Equal(expected.R, frames[frameIndex][0]);
            Assert.Equal(expected.G, frames[frameIndex][1]);
            Assert.Equal(expected.B, frames[frameIndex][2]);
            Assert.Equal(expected.A, frames[frameIndex][3]);
        }
    }

    private static byte[] Encode(int frameCount, bool webp, int side = Side)
    {
        using var image = new Image<Rgba32>(side, side, Colors[0]);
        SetDelay(image.Frames.RootFrame, webp);
        for (var frameIndex = 1; frameIndex < frameCount; frameIndex++)
        {
            using var next = new Image<Rgba32>(side, side, Colors[frameIndex % Colors.Length]);
            SetDelay(image.Frames.AddFrame(next.Frames.RootFrame), webp);
        }

        using var stream = new MemoryStream();
        if (webp)
        {
            image.SaveAsWebp(stream, new WebpEncoder { FileFormat = WebpFileFormatType.Lossless });
        }
        else
        {
            image.SaveAsPng(stream);
        }

        return stream.ToArray();
    }

    private static void SetDelay(ImageFrame<Rgba32> frame, bool webp)
    {
        if (webp)
        {
            frame.Metadata.GetWebpMetadata().FrameDelay = DelayMilliseconds;
            return;
        }

        frame.Metadata.GetPngMetadata().FrameDelay = new Rational(DelayMilliseconds, 1000);
    }
}
