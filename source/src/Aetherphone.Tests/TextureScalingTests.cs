using Aetherphone.Core.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TextureScalingTests
{
    [Fact]
    public void ALevelCoversTheSizeItIsDrawnAt()
    {
        Assert.Equal(32, TextureSizes.SizeOf(TextureSizes.LevelFor(1f)));
        Assert.Equal(32, TextureSizes.SizeOf(TextureSizes.LevelFor(32f)));
        Assert.Equal(64, TextureSizes.SizeOf(TextureSizes.LevelFor(33f)));
        Assert.Equal(64, TextureSizes.SizeOf(TextureSizes.LevelFor(64f)));
        Assert.Equal(128, TextureSizes.SizeOf(TextureSizes.LevelFor(100f)));
        Assert.Equal(256, TextureSizes.SizeOf(TextureSizes.LevelFor(200f)));
        Assert.Equal(512, TextureSizes.SizeOf(TextureSizes.LevelFor(512f)));
    }

    [Fact]
    public void TheLargestLevelIsTheCeilingForOversizedDraws()
    {
        Assert.Equal(512, TextureSizes.SizeOf(TextureSizes.LevelFor(4000f)));
        Assert.Equal(TextureSizes.LevelCount, TextureSizes.LevelFor(4000f));
    }

    [Fact]
    public void NativeIsDistinctFromEverySizedLevel()
    {
        Assert.Equal(0, TextureSizes.SizeOf(TextureSizes.Native));
        for (var level = 1; level <= TextureSizes.LevelCount; level++)
        {
            Assert.NotEqual(TextureSizes.Native, level);
            Assert.True(TextureSizes.SizeOf(level) > 0);
        }
    }

    [Fact]
    public void DecodingWithoutALimitKeepsTheSourceResolution()
    {
        var png = SquarePng(512);
        var (_, width, height) = ImageProcessor.DecodeRgba32(png);

        Assert.Equal(512, width);
        Assert.Equal(512, height);
    }

    [Fact]
    public void DecodingToALimitShrinksTheSourceToIt()
    {
        var png = SquarePng(512);
        var (pixels, width, height) = ImageProcessor.DecodeRgba32(png, 64);

        Assert.Equal(64, width);
        Assert.Equal(64, height);
        Assert.Equal(64 * 64 * 4, pixels.Length);
    }

    [Fact]
    public void DecodingToALimitNeverEnlargesASmallSource()
    {
        var png = SquarePng(48);
        var (_, width, height) = ImageProcessor.DecodeRgba32(png, 256);

        Assert.Equal(48, width);
        Assert.Equal(48, height);
    }

    [Fact]
    public void ShrinkingAnAlphaEdgeDoesNotDarkenTheColourUnderIt()
    {
        using var source = new Image<Rgba32>(64, 64);
        source.ProcessPixelRows(accessor =>
        {
            for (var rowIndex = 0; rowIndex < accessor.Height; rowIndex++)
            {
                var row = accessor.GetRowSpan(rowIndex);
                for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    row[columnIndex] = columnIndex < 32
                        ? new Rgba32(255, 255, 255, 255)
                        : new Rgba32(0, 0, 0, 0);
                }
            }
        });

        using var encoded = new MemoryStream();
        source.SaveAsPng(encoded);
        var (pixels, width, _) = ImageProcessor.DecodeRgba32(encoded.ToArray(), 8);

        var faded = 0;
        for (var columnIndex = 0; columnIndex < width; columnIndex++)
        {
            var offset = columnIndex * 4;
            if (pixels[offset + 3] is > 0 and < 250)
            {
                faded++;
                Assert.True(pixels[offset] > 200,
                    $"column {columnIndex} lost colour: rgba({pixels[offset]}, {pixels[offset + 1]}, " +
                    $"{pixels[offset + 2]}, {pixels[offset + 3]})");
            }
        }

        Assert.True(faded > 0, "the downscale produced no partially transparent edge to check");
    }

    private static byte[] SquarePng(int size)
    {
        using var image = new Image<Rgba32>(size, size);
        image.ProcessPixelRows(accessor =>
        {
            for (var rowIndex = 0; rowIndex < accessor.Height; rowIndex++)
            {
                var row = accessor.GetRowSpan(rowIndex);
                for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    row[columnIndex] = new Rgba32((byte)columnIndex, (byte)rowIndex, 128, 255);
                }
            }
        });

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
