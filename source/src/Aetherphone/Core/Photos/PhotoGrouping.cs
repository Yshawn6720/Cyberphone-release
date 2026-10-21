namespace Aetherphone.Core.Photos;

internal readonly record struct PhotoRun(int Key, int Start, int Count);

internal readonly record struct PhotoBucket(uint Key, int[] Indices);

internal static class PhotoGrouping
{
    public static int MonthKey(DateTime taken) => taken.Year * 100 + taken.Month;

    public static int YearOfMonthKey(int monthKey) => monthKey / 100;

    public static void Runs(ReadOnlySpan<int> keys, List<PhotoRun> runs)
    {
        runs.Clear();
        var index = 0;
        while (index < keys.Length)
        {
            var key = keys[index];
            var start = index;
            while (index < keys.Length && keys[index] == key)
            {
                index++;
            }

            runs.Add(new PhotoRun(key, start, index - start));
        }
    }

    public static PhotoBucket[] Buckets(ReadOnlySpan<uint> keys)
    {
        var order = new List<uint>();
        var members = new Dictionary<uint, List<int>>();
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            if (key == 0)
            {
                continue;
            }

            if (!members.TryGetValue(key, out var list))
            {
                list = new List<int>();
                members[key] = list;
                order.Add(key);
            }

            list.Add(index);
        }

        var buckets = new PhotoBucket[order.Count];
        for (var bucketIndex = 0; bucketIndex < order.Count; bucketIndex++)
        {
            var key = order[bucketIndex];
            buckets[bucketIndex] = new PhotoBucket(key, members[key].ToArray());
        }

        return buckets;
    }

    public static int FirstRunAtOrAfter(List<PhotoRun> runs, int key, bool descending)
    {
        for (var index = 0; index < runs.Count; index++)
        {
            var runKey = runs[index].Key;
            if (runKey == key || (descending ? runKey < key : runKey > key))
            {
                return index;
            }
        }

        return runs.Count - 1;
    }
}
