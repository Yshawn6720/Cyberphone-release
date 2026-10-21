namespace Aetherphone.Core.Message;

internal sealed class ViewingMark
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(4);

    private volatile string? viewedKey;
    private long viewedAtTicks;

    public void Note(string key) => Note(key, DateTime.UtcNow);

    public void Note(string key, DateTime nowUtc)
    {
        Volatile.Write(ref viewedAtTicks, nowUtc.Ticks);
        viewedKey = key;
    }

    public bool Covers(string key) => Covers(key, DateTime.UtcNow);

    public bool Covers(string key, DateTime nowUtc) =>
        string.Equals(viewedKey, key, StringComparison.Ordinal)
        && nowUtc.Ticks - Volatile.Read(ref viewedAtTicks) < Grace.Ticks;

    public void Clear() => viewedKey = null;
}
