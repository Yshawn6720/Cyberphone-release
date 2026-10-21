using Aetherphone.Core;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Health;

internal struct HealthHighlight
{
    public int Metric;
    public string Text;
    public string RecentValue;
    public string PreviousValue;
    public float RecentFraction;
    public float PreviousFraction;
    public bool IsWeight;
}

internal sealed class HealthDigest
{
    public const int Days = HealthSeries.MonthDays;
    public const int TodaySlot = Days - 1;
    public const int MaxHighlights = 3;
    public const int WeightSparkBuckets = 30;
    public const double WeightSteadyKilograms = 0.1;

    private const long DaySeconds = 86_400L;
    private const int WeightHighlightDays = 30;
    private const int WeightHighlightMinSpanDays = 7;

    public readonly double[] Values = new double[HealthSeries.MetricCount * Days];
    public readonly bool[] Recorded = new bool[HealthSeries.MetricCount * Days];
    public readonly DateTime[] Dates = new DateTime[Days];
    public readonly string[] DayTitles = new string[Days];
    public readonly string[] ShortDates = new string[Days];
    public readonly string[] WeekLetters = new string[HealthSeries.WeekDays];
    public readonly HealthHighlight[] Highlights = new HealthHighlight[MaxHighlights];
    public readonly float[] WeightSpark = new float[WeightSparkBuckets];
    public readonly bool[] WeightSparkPresent = new bool[WeightSparkBuckets];

    private long key;
    private bool built;
    private DateTime todayDate;
    private string todayKey = string.Empty;

    public int HighlightCount { get; private set; }
    public HealthDay? Today { get; private set; }
    public WeightEntry? LatestWeight { get; private set; }
    public string LatestWeightDate { get; private set; } = string.Empty;
    public float WeightSparkLow { get; private set; }
    public float WeightSparkHigh { get; private set; }

    public void Invalidate() => built = false;

    public void Refresh(HealthTracker tracker)
    {
        var date = DateTime.Today;
        if (date != todayDate || todayKey.Length == 0)
        {
            todayDate = date;
            todayKey = HealthSeries.DateKey(date);
        }

        var profile = tracker.Profile;
        var latest = profile.LatestDay;
        Today = latest is not null && latest.Date == todayKey ? latest : null;
        var candidate = KeyFor(tracker);
        if (!built || candidate != key)
        {
            key = candidate;
            built = true;
            Build(tracker);
        }

        for (var metric = 0; metric < HealthSeries.MetricCount; metric++)
        {
            var slot = metric * Days + TodaySlot;
            Recorded[slot] = Today is not null;
            Values[slot] = Today is null ? 0d : HealthSeries.DayValue(Today, (HealthMetric)metric, profile.StrideYalms);
        }
    }

    public ReadOnlySpan<double> Series(HealthMetric metric, int length) =>
        Values.AsSpan((int)metric * Days + Days - length, length);

    public ReadOnlySpan<bool> SeriesRecorded(HealthMetric metric, int length) =>
        Recorded.AsSpan((int)metric * Days + Days - length, length);

    public double Live(HealthMetric metric) => Values[(int)metric * Days + TodaySlot];

    private long KeyFor(HealthTracker tracker)
    {
        var profile = tracker.Profile;
        var contextHash = HashCode.Combine(tracker.Revision, profile.Days.Count, profile.WeightLog.Count,
            profile.Units, profile.StrideYalms, todayDate, Loc.Culture, TimeText.FormatVersion);
        return ((long)contextHash << 32) ^ (uint)HashCode.Combine(tracker.CharacterId, profile.DailyStepGoal,
            profile.DailyHydrationGoal);
    }

    private void Build(HealthTracker tracker)
    {
        var profile = tracker.Profile;
        for (var slot = 0; slot < Days; slot++)
        {
            var date = todayDate.AddDays(slot - TodaySlot);
            Dates[slot] = date;
            DayTitles[slot] = HealthText.DayTitle(date);
            ShortDates[slot] = date.ToString("MMM d", Loc.Culture);
        }

        for (var slot = 0; slot < HealthSeries.WeekDays; slot++)
        {
            var date = todayDate.AddDays(slot - (HealthSeries.WeekDays - 1));
            WeekLetters[slot] = Loc.Culture.DateTimeFormat.GetShortestDayName(date.DayOfWeek);
        }

        for (var metric = 0; metric < HealthSeries.MetricCount; metric++)
        {
            HealthSeries.Fill(profile.Days, (HealthMetric)metric, profile.StrideYalms, todayDate,
                Values.AsSpan(metric * Days, Days), Recorded.AsSpan(metric * Days, Days));
        }

        BuildWeight(profile);
        BuildHighlights(profile);
    }

    private void BuildWeight(HealthProfile profile)
    {
        var log = profile.WeightLog;
        LatestWeight = log.Count > 0 ? log[log.Count - 1] : null;
        LatestWeightDate = LatestWeight is null ? string.Empty : TimeText.DayLabel(LatestWeight.Unix);
        var from = new DateTimeOffset(todayDate).ToUnixTimeSeconds() + DaySeconds - WeightSparkBuckets * DaySeconds;
        WeightHistory.Bucket(log, from, DaySeconds, WeightSpark, WeightSparkPresent);
        var low = float.MaxValue;
        var high = float.MinValue;
        for (var bucket = 0; bucket < WeightSparkBuckets; bucket++)
        {
            if (!WeightSparkPresent[bucket])
            {
                continue;
            }

            low = MathF.Min(low, WeightSpark[bucket]);
            high = MathF.Max(high, WeightSpark[bucket]);
        }

        WeightSparkLow = low <= high ? low - 0.5f : 0f;
        WeightSparkHigh = low <= high ? high + 0.5f : 1f;
    }

    private void BuildHighlights(HealthProfile profile)
    {
        HighlightCount = 0;
        AddWeightHighlight(profile);
        AddTrendHighlight(profile, HealthMetric.Water);
        AddTrendHighlight(profile, HealthMetric.Steps);
        AddTrendHighlight(profile, HealthMetric.ActiveTime);
        AddTrendHighlight(profile, HealthMetric.Swimming);
    }

    private void AddTrendHighlight(HealthProfile profile, HealthMetric metric)
    {
        if (HighlightCount >= MaxHighlights)
        {
            return;
        }

        var trend = HealthSeries.Trend(profile.Days, metric, profile.StrideYalms, todayDate);
        if (!trend.Available || trend.Direction == TrendDirection.Steady)
        {
            return;
        }

        var up = trend.Direction == TrendDirection.Up;
        var text = Loc.T(metric switch
        {
            HealthMetric.Water => up ? L.Health.HighlightWaterUp : L.Health.HighlightWaterDown,
            HealthMetric.Steps => up ? L.Health.HighlightStepsUp : L.Health.HighlightStepsDown,
            HealthMetric.ActiveTime => up ? L.Health.HighlightActiveUp : L.Health.HighlightActiveDown,
            _ => up ? L.Health.HighlightSwimUp : L.Health.HighlightSwimDown,
        });
        var peak = Math.Max(trend.Recent, trend.Previous);
        Highlights[HighlightCount++] = new HealthHighlight
        {
            Metric = (int)metric,
            Text = text,
            RecentValue = HealthText.Joined(metric, trend.Recent, profile.Units, true),
            PreviousValue = HealthText.Joined(metric, trend.Previous, profile.Units, true),
            RecentFraction = peak > 0d ? (float)(trend.Recent / peak) : 0f,
            PreviousFraction = peak > 0d ? (float)(trend.Previous / peak) : 0f,
            IsWeight = false,
        };
    }

    private void AddWeightHighlight(HealthProfile profile)
    {
        var log = profile.WeightLog;
        if (log.Count < 2)
        {
            return;
        }

        var from = new DateTimeOffset(todayDate).ToUnixTimeSeconds() - WeightHighlightDays * DaySeconds;
        var first = WeightHistory.FirstAtOrAfter(log, from);
        if (log.Count - first < 2)
        {
            return;
        }

        var start = log[first];
        var end = log[log.Count - 1];
        if (end.Unix - start.Unix < WeightHighlightMinSpanDays * DaySeconds)
        {
            return;
        }

        var change = end.Kilograms - start.Kilograms;
        var steady = Math.Abs(change) < WeightSteadyKilograms;
        var text = steady
            ? Loc.T(L.Health.HighlightWeightSteady)
            : Loc.T(L.Health.HighlightWeightChange, HealthFormat.WeightChange(change, profile.Units));
        var peak = Math.Max(start.Kilograms, end.Kilograms);
        Highlights[HighlightCount++] = new HealthHighlight
        {
            Metric = -1,
            Text = text,
            RecentValue = HealthFormat.WeightPrecise(end.Kilograms, profile.Units),
            PreviousValue = HealthFormat.WeightPrecise(start.Kilograms, profile.Units),
            RecentFraction = (float)(end.Kilograms / peak),
            PreviousFraction = (float)(start.Kilograms / peak),
            IsWeight = true,
        };
    }
}
