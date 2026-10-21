namespace Aetherphone.Core.SystemMedia;

internal sealed class MediaSessionActivity
{
    public const long RecentMilliseconds = 15L * 60L * 1000L;

    private const int Capacity = 32;

    private readonly string[] appIds = new string[Capacity];
    private readonly long[] playedAt = new long[Capacity];
    private int count;

    public void Observe(string appId, bool playing, long now)
    {
        if (!playing || appId.Length == 0)
        {
            return;
        }

        var index = IndexOf(appId);
        if (index < 0)
        {
            index = Claim();
            appIds[index] = appId;
        }

        playedAt[index] = now;
    }

    public bool IsRecent(string appId, bool playing, long now)
    {
        if (playing)
        {
            return true;
        }

        var index = IndexOf(appId);
        return index >= 0 && now - playedAt[index] < RecentMilliseconds;
    }

    private int IndexOf(string appId)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(appIds[index], appId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private int Claim()
    {
        if (count < Capacity)
        {
            return count++;
        }

        var oldest = 0;
        for (var index = 1; index < Capacity; index++)
        {
            if (playedAt[index] < playedAt[oldest])
            {
                oldest = index;
            }
        }

        return oldest;
    }
}
