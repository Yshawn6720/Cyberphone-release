namespace Aetherphone.Core.Health;

internal enum WeightRange : byte
{
    Week,
    Month,
    HalfYear,
    Year,
}

internal static class WeightHistory
{
    public const int MaxEntries = 1500;
    public const double MinKilograms = 1d;
    public const double MaxKilograms = 650d;
    public const double DefaultKilograms = 70d;
    public const int RangeCount = (int)WeightRange.Year + 1;
    public const int MaxBuckets = 52;

    private const long DaySeconds = 86_400L;
    private const long WeekSeconds = DaySeconds * 7L;

    public static bool IsValid(double kilograms) =>
        double.IsFinite(kilograms) && kilograms >= MinKilograms && kilograms <= MaxKilograms;

    public static int BucketCount(WeightRange range) => range switch
    {
        WeightRange.Week => 7,
        WeightRange.Month => 30,
        WeightRange.HalfYear => 26,
        _ => MaxBuckets,
    };

    public static long BucketSeconds(WeightRange range) =>
        range is WeightRange.Week or WeightRange.Month ? DaySeconds : WeekSeconds;

    public static long RangeStart(WeightRange range, DateTimeOffset localToday)
    {
        var startOfToday = new DateTimeOffset(localToday.Date, localToday.Offset).ToUnixTimeSeconds();
        var buckets = BucketCount(range);
        var span = BucketSeconds(range);
        return startOfToday + DaySeconds - buckets * span;
    }

    public static void Insert(List<WeightEntry> entries, WeightEntry entry)
    {
        var index = entries.Count;
        while (index > 0 && entries[index - 1].Unix > entry.Unix)
        {
            index--;
        }

        entries.Insert(index, entry);
        while (entries.Count > MaxEntries)
        {
            entries.RemoveAt(0);
        }
    }

    public static void Normalize(List<WeightEntry> entries)
    {
        entries.RemoveAll(entry => entry is null || entry.Unix <= 0 || !IsValid(entry.Kilograms));
        entries.Sort(static (left, right) => left.Unix.CompareTo(right.Unix));
        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(0, entries.Count - MaxEntries);
        }
    }

    public static double? Latest(IReadOnlyList<WeightEntry> entries) =>
        entries.Count > 0 ? entries[entries.Count - 1].Kilograms : null;

    public static void Adopt(List<WeightEntry> entries, double? legacyKilograms, long legacyUnix)
    {
        if (entries.Count > 0 || legacyKilograms is not { } kilograms || !IsValid(kilograms) || legacyUnix <= 0)
        {
            return;
        }

        entries.Add(new WeightEntry { Unix = legacyUnix, Kilograms = kilograms });
    }

    public static int FirstAtOrAfter(IReadOnlyList<WeightEntry> entries, long unix)
    {
        var low = 0;
        var high = entries.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (entries[middle].Unix < unix)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    public static int Bucket(IReadOnlyList<WeightEntry> entries, long fromUnix, long bucketSeconds,
        Span<float> values, Span<bool> present)
    {
        var count = Math.Min(values.Length, present.Length);
        Span<double> sums = stackalloc double[count];
        Span<int> tallies = stackalloc int[count];
        var toUnix = fromUnix + bucketSeconds * count;
        for (var index = FirstAtOrAfter(entries, fromUnix); index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.Unix >= toUnix)
            {
                break;
            }

            var bucket = (int)((entry.Unix - fromUnix) / bucketSeconds);
            sums[bucket] += entry.Kilograms;
            tallies[bucket]++;
        }

        var filled = 0;
        for (var bucket = 0; bucket < count; bucket++)
        {
            present[bucket] = tallies[bucket] > 0;
            values[bucket] = tallies[bucket] > 0 ? (float)(sums[bucket] / tallies[bucket]) : 0f;
            if (present[bucket])
            {
                filled++;
            }
        }

        return filled;
    }

    public static double? ChangeSince(IReadOnlyList<WeightEntry> entries, long fromUnix)
    {
        var first = FirstAtOrAfter(entries, fromUnix);
        if (entries.Count - first < 2)
        {
            return null;
        }

        return entries[entries.Count - 1].Kilograms - entries[first].Kilograms;
    }

    public static double Snap(double value, double step)
    {
        if (step <= 0d || !double.IsFinite(value))
        {
            return value;
        }

        return Math.Round(value / step) * step;
    }
}
