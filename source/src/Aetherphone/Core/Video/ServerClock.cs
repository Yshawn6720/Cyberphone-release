namespace Aetherphone.Core.Video;

internal sealed class ServerClock
{
    internal const int SampleWindow = 8;

    private readonly long[] samples = new long[SampleWindow];
    private readonly object gate = new();
    private int sampleCount;
    private int nextSampleIndex;
    private long skewMilliseconds;

    internal bool Anchored => Volatile.Read(ref sampleCount) > 0;

    internal long SkewMilliseconds => Volatile.Read(ref skewMilliseconds);

    internal long ServerNowUnixMs(long localNowUnixMs) => localNowUnixMs + SkewMilliseconds;

    internal void Absorb(long serverStampUnixMs, long localReceivedUnixMs)
    {
        if (serverStampUnixMs <= 0)
        {
            return;
        }

        lock (gate)
        {
            samples[nextSampleIndex] = serverStampUnixMs - localReceivedUnixMs;
            nextSampleIndex = (nextSampleIndex + 1) % SampleWindow;
            var count = Math.Min(sampleCount + 1, SampleWindow);
            var best = long.MinValue;
            for (var index = 0; index < count; index++)
            {
                best = Math.Max(best, samples[index]);
            }

            Volatile.Write(ref skewMilliseconds, best);
            Volatile.Write(ref sampleCount, count);
        }
    }

    internal void Reset()
    {
        lock (gate)
        {
            Volatile.Write(ref sampleCount, 0);
            nextSampleIndex = 0;
            Volatile.Write(ref skewMilliseconds, 0);
        }
    }
}
