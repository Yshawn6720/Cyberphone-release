using System.Collections.Concurrent;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal readonly record struct LedgerKey(string Name, int Level);

internal sealed class TextureLedger
{
    private sealed class Entry : IDisposable
    {
        public readonly IDalamudTextureWrap? Wrap;
        public readonly AnimatedImage? Animation;
        public readonly long Bytes;
        public long LastAccessTicks;

        public Entry(IDalamudTextureWrap wrap)
        {
            Wrap = wrap;
            Bytes = (long)wrap.Width * wrap.Height * 4;
            LastAccessTicks = Environment.TickCount64;
        }

        public Entry(AnimatedImage animation)
        {
            Animation = animation;
            Bytes = animation.Bytes;
            LastAccessTicks = Environment.TickCount64;
        }

        public void Dispose()
        {
            Wrap?.Dispose();
            Animation?.Dispose();
        }
    }

    private static readonly TimeSpan EvictionIdleFloor = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<LedgerKey, Entry> entries = new();
    private readonly long budgetBytes;
    private long totalBytes;

    public TextureLedger(long budgetBytes)
    {
        this.budgetBytes = budgetBytes;
    }

    public IDalamudTextureWrap? Get(string name) => Get(new LedgerKey(name, TextureSizes.Native));

    public IDalamudTextureWrap? Get(string name, int level) => Get(new LedgerKey(name, level));

    public IDalamudTextureWrap? Get(LedgerKey key)
    {
        return GetAt(key, 0d);
    }

    public IDalamudTextureWrap? GetAt(LedgerKey key, double timeSeconds)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        Volatile.Write(ref entry.LastAccessTicks, Environment.TickCount64);
        return entry.Wrap ?? entry.Animation!.FrameAt(timeSeconds);
    }

    public IDalamudTextureWrap? Nearest(string name, int level)
    {
        return Nearest(name, level, 0d);
    }

    public IDalamudTextureWrap? Nearest(string name, int level, double timeSeconds)
    {
        for (var above = level + 1; above <= TextureSizes.LevelCount; above++)
        {
            if (GetAt(new LedgerKey(name, above), timeSeconds) is { } larger)
            {
                return larger;
            }
        }

        for (var below = level - 1; below > TextureSizes.Native; below--)
        {
            if (GetAt(new LedgerKey(name, below), timeSeconds) is { } smaller)
            {
                return smaller;
            }
        }

        return GetAt(new LedgerKey(name, TextureSizes.Native), timeSeconds);
    }

    public AnimatedImage? GetAnimated(string name)
    {
        if (!entries.TryGetValue(new LedgerKey(name, TextureSizes.Native), out var entry) || entry.Animation is null)
        {
            return null;
        }

        Volatile.Write(ref entry.LastAccessTicks, Environment.TickCount64);
        return entry.Animation;
    }

    public Vector2 SizeOf(string name)
    {
        if (!entries.TryGetValue(new LedgerKey(name, TextureSizes.Native), out var entry))
        {
            return Vector2.Zero;
        }

        return entry.Wrap?.Size ?? entry.Animation!.Frames[0].Size;
    }

    public bool TryAdd(string name, IDalamudTextureWrap wrap)
    {
        return TryAddEntry(new LedgerKey(name, TextureSizes.Native), new Entry(wrap));
    }

    public bool TryAdd(LedgerKey key, IDalamudTextureWrap wrap)
    {
        return TryAddEntry(key, new Entry(wrap));
    }

    public bool TryAddAnimated(string name, AnimatedImage animation)
    {
        return TryAddEntry(new LedgerKey(name, TextureSizes.Native), new Entry(animation));
    }

    public bool TryAddAnimated(LedgerKey key, AnimatedImage animation)
    {
        return TryAddEntry(key, new Entry(animation));
    }

    private bool TryAddEntry(LedgerKey key, Entry entry)
    {
        if (!entries.TryAdd(key, entry))
        {
            return false;
        }

        Interlocked.Add(ref totalBytes, entry.Bytes);
        EvictPastBudget();
        return true;
    }

    public bool TryRemove(string name, out IDisposable disposable)
    {
        if (entries.TryRemove(new LedgerKey(name, TextureSizes.Native), out var entry))
        {
            Interlocked.Add(ref totalBytes, -entry.Bytes);
            disposable = entry;
            return true;
        }

        disposable = null!;
        return false;
    }

    public void RemoveAndDispose(string name) => RemoveAndDispose(new LedgerKey(name, TextureSizes.Native));

    public void RemoveAndDispose(LedgerKey key)
    {
        if (entries.TryRemove(key, out var entry))
        {
            Interlocked.Add(ref totalBytes, -entry.Bytes);
            entry.Dispose();
        }
    }

    public void DisposeAll()
    {
        foreach (var key in entries.Keys)
        {
            if (entries.TryRemove(key, out var entry))
            {
                Interlocked.Add(ref totalBytes, -entry.Bytes);
                entry.Dispose();
            }
        }
    }

    private void EvictPastBudget()
    {
        if (Interlocked.Read(ref totalBytes) <= budgetBytes)
        {
            return;
        }

        var now = Environment.TickCount64;
        var idleFloorMs = (long)EvictionIdleFloor.TotalMilliseconds;
        var candidates = new List<KeyValuePair<LedgerKey, Entry>>();
        foreach (var pair in entries)
        {
            if (now - Volatile.Read(ref pair.Value.LastAccessTicks) >= idleFloorMs)
            {
                candidates.Add(pair);
            }
        }

        candidates.Sort(static (left, right) =>
            Volatile.Read(ref left.Value.LastAccessTicks).CompareTo(Volatile.Read(ref right.Value.LastAccessTicks)));

        for (var index = 0; index < candidates.Count; index++)
        {
            if (Interlocked.Read(ref totalBytes) <= budgetBytes)
            {
                return;
            }

            if (entries.TryRemove(candidates[index].Key, out var removed))
            {
                Interlocked.Add(ref totalBytes, -removed.Bytes);
                _ = Plugin.Framework.RunOnFrameworkThread(removed.Dispose);
            }
        }
    }
}
