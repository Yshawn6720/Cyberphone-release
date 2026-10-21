namespace Aetherphone.Core.Photos;

internal readonly record struct RgbaImage(byte[] Pixels, int Width, int Height);

internal static class PhotoPixels
{
    private const int Channels = 4;

    public static RgbaImage Downsample(byte[] pixels, int width, int height, int maxDimension)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height * Channels)
        {
            return new RgbaImage(Array.Empty<byte>(), 0, 0);
        }

        var factor = (int)MathF.Ceiling(MathF.Max(width, height) / (float)Math.Max(1, maxDimension));
        if (factor <= 1)
        {
            return new RgbaImage(pixels, width, height);
        }

        var targetWidth = Math.Max(1, width / factor);
        var targetHeight = Math.Max(1, height / factor);
        var result = new byte[targetWidth * targetHeight * Channels];
        var area = factor * factor;
        for (var targetRow = 0; targetRow < targetHeight; targetRow++)
        {
            for (var targetColumn = 0; targetColumn < targetWidth; targetColumn++)
            {
                var red = 0;
                var green = 0;
                var blue = 0;
                var alpha = 0;
                for (var sampleRow = 0; sampleRow < factor; sampleRow++)
                {
                    var sourceOffset = ((targetRow * factor + sampleRow) * width + targetColumn * factor) * Channels;
                    for (var sampleColumn = 0; sampleColumn < factor; sampleColumn++)
                    {
                        var offset = sourceOffset + sampleColumn * Channels;
                        red += pixels[offset];
                        green += pixels[offset + 1];
                        blue += pixels[offset + 2];
                        alpha += pixels[offset + 3];
                    }
                }

                var destination = (targetRow * targetWidth + targetColumn) * Channels;
                result[destination] = (byte)(red / area);
                result[destination + 1] = (byte)(green / area);
                result[destination + 2] = (byte)(blue / area);
                result[destination + 3] = (byte)(alpha / area);
            }
        }

        return new RgbaImage(result, targetWidth, targetHeight);
    }
}
