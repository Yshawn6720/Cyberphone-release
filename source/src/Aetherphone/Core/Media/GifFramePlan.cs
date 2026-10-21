namespace Aetherphone.Core.Media;

internal static class GifFramePlan
{
    public const float MinRawDelaySeconds = 0.02f;
    public const float SlowFallbackDelaySeconds = 0.1f;

    public static float NormalizeDelay(float rawSeconds)
    {
        return rawSeconds < MinRawDelaySeconds ? SlowFallbackDelaySeconds : rawSeconds;
    }

    public static int KeepEvery(int frameCount, int width, int height, long maxTotalPixels)
    {
        if (frameCount <= 1 || width <= 0 || height <= 0 || maxTotalPixels <= 0)
        {
            return 1;
        }

        var totalPixels = (long)frameCount * width * height;
        if (totalPixels <= maxTotalPixels)
        {
            return 1;
        }

        var framesThatFit = Math.Max(1L, maxTotalPixels / ((long)width * height));
        return (int)((frameCount + framesThatFit - 1) / framesThatFit);
    }

    public static (int[] KeptIndices, float[] Delays) Plan(float[] rawDelays, int width, int height,
        long maxTotalPixels)
    {
        var keepEvery = KeepEvery(rawDelays.Length, width, height, maxTotalPixels);
        var keptCount = Math.Max(1, (rawDelays.Length + keepEvery - 1) / keepEvery);
        var keptIndices = new int[keptCount];
        var delays = new float[keptCount];
        for (var keptIndex = 0; keptIndex < keptCount; keptIndex++)
        {
            var start = keptIndex * keepEvery;
            keptIndices[keptIndex] = start;
            var merged = 0f;
            var end = Math.Min(start + keepEvery, rawDelays.Length);
            for (var frameIndex = start; frameIndex < end; frameIndex++)
            {
                merged += NormalizeDelay(rawDelays[frameIndex]);
            }

            delays[keptIndex] = merged;
        }

        return (keptIndices, delays);
    }

    public static int FrameIndexAt(float[] delays, float totalSeconds, double timeSeconds)
    {
        if (delays.Length <= 1 || totalSeconds <= 0f)
        {
            return 0;
        }

        var wrapped = (float)(timeSeconds % totalSeconds);
        var accumulated = 0f;
        for (var index = 0; index < delays.Length; index++)
        {
            accumulated += delays[index];
            if (wrapped < accumulated)
            {
                return index;
            }
        }

        return delays.Length - 1;
    }
}
