using Aetherphone.Core;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Health;

internal sealed class WeightDigest
{
    public const int MaxRows = 30;

    private const float Padding = 0.6f;
    private const float MinimumSpan = 2f;

    public readonly float[] Values = new float[WeightHistory.MaxBuckets];
    public readonly bool[] Present = new bool[WeightHistory.MaxBuckets];
    public readonly string[] BucketLabels = new string[WeightHistory.MaxBuckets];
    public readonly string[] BucketValues = new string[WeightHistory.MaxBuckets];
    public readonly string[] RowDates = new string[MaxRows];
    public readonly string[] RowValues = new string[MaxRows];
    public readonly WeightEntry?[] RowEntries = new WeightEntry?[MaxRows];

    private long key;
    private bool built;

    public int Buckets { get; private set; }
    public int Filled { get; private set; }
    public int Rows { get; private set; }
    public float Low { get; private set; }
    public float High { get; private set; }
    public float DataLow { get; private set; }
    public float DataHigh { get; private set; }
    public string LowText { get; private set; } = string.Empty;
    public string HighText { get; private set; } = string.Empty;
    public string LatestValue { get; private set; } = string.Empty;
    public string LatestDate { get; private set; } = string.Empty;
    public string ChangeText { get; private set; } = string.Empty;
    public string AxisStart { get; private set; } = string.Empty;

    public void Invalidate() => built = false;

    public void Refresh(HealthTracker tracker, WeightRange range)
    {
        var profile = tracker.Profile;
        var candidate = ((long)HashCode.Combine(tracker.Revision, profile.WeightLog.Count, profile.Units, DateTime.Today,
            Loc.Culture, TimeText.FormatVersion, tracker.CharacterId) << 8) | (byte)range;
        if (built && candidate == key)
        {
            return;
        }

        key = candidate;
        built = true;
        Build(profile, range);
    }

    private void Build(HealthProfile profile, WeightRange range)
    {
        var log = profile.WeightLog;
        var units = profile.Units;
        Buckets = WeightHistory.BucketCount(range);
        var bucketSeconds = WeightHistory.BucketSeconds(range);
        var from = WeightHistory.RangeStart(range, DateTimeOffset.Now);
        Filled = WeightHistory.Bucket(log, from, bucketSeconds, Values.AsSpan(0, Buckets), Present.AsSpan(0, Buckets));
        var low = float.MaxValue;
        var high = float.MinValue;
        var weekly = bucketSeconds > 86_400L;
        for (var bucket = 0; bucket < Buckets; bucket++)
        {
            var start = from + bucketSeconds * bucket;
            var day = TimeText.MonthDay(start);
            BucketLabels[bucket] = weekly ? Loc.T(L.Health.WeekOf, day) : day;
            BucketValues[bucket] = Present[bucket] ? HealthFormat.WeightPrecise(Values[bucket], units) : string.Empty;
            if (!Present[bucket])
            {
                continue;
            }

            low = MathF.Min(low, Values[bucket]);
            high = MathF.Max(high, Values[bucket]);
        }

        if (Filled > 0)
        {
            var span = MathF.Max(MinimumSpan, high - low);
            var middle = (high + low) * 0.5f;
            Low = middle - span * (0.5f + Padding * 0.5f);
            High = middle + span * (0.5f + Padding * 0.5f);
            DataLow = low;
            DataHigh = high;
            LowText = HealthFormat.WeightNumber(low, units);
            HighText = HealthFormat.WeightNumber(high, units);
        }
        else
        {
            Low = 0f;
            High = 1f;
            LowText = string.Empty;
            HighText = string.Empty;
        }

        var latest = log.Count > 0 ? log[log.Count - 1] : null;
        LatestValue = latest is null ? string.Empty : HealthFormat.WeightNumber(latest.Kilograms, units);
        LatestDate = latest is null ? string.Empty : TimeText.DayLabel(latest.Unix);
        var change = WeightHistory.ChangeSince(log, from);
        ChangeText = change is { } delta
            ? Math.Abs(delta) < HealthDigest.WeightSteadyKilograms
                ? Loc.T(L.Health.SteadyInRange)
                : Loc.T(L.Health.ChangeInRange, HealthFormat.WeightChange(delta, units))
            : string.Empty;
        AxisStart = TimeText.MonthDay(from);
        Rows = Math.Min(MaxRows, log.Count);
        for (var row = 0; row < Rows; row++)
        {
            var entry = log[log.Count - 1 - row];
            RowEntries[row] = entry;
            RowDates[row] = Loc.T(L.Health.DateAtTime, TimeText.DayLabel(entry.Unix), TimeText.Clock(entry.Unix));
            RowValues[row] = HealthFormat.WeightPrecise(entry.Kilograms, units);
        }

        for (var row = Rows; row < MaxRows; row++)
        {
            RowEntries[row] = null;
        }
    }
}
