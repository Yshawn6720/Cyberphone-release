using System.Globalization;

namespace Aetherphone.Core.Health;

internal enum HealthMetric : byte
{
    Water,
    Steps,
    Distance,
    Swimming,
    ActiveTime,
}

internal enum TrendDirection : byte
{
    Steady,
    Up,
    Down,
}

internal readonly record struct HealthTrend(bool Available, double Recent, double Previous, TrendDirection Direction);

internal static class HealthSeries
{
    public const string DateFormat = "yyyy-MM-dd";
    public const int WeekDays = 7;
    public const int MonthDays = 30;
    public const int MetricCount = (int)HealthMetric.ActiveTime + 1;
    public const int MinTrendDays = 3;
    public const double SteadyFraction = 0.05;

    public static string DateKey(DateTime date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static double DayValue(HealthDay day, HealthMetric metric, double strideYalms) => metric switch
    {
        HealthMetric.Water => day.DrinkCount,
        HealthMetric.Steps => HealthFormat.Steps(day.OnFootYalms, strideYalms),
        HealthMetric.Distance => day.OnFootYalms,
        HealthMetric.Swimming => day.SwimTotalYalms,
        _ => day.ActiveSeconds,
    };

    public static HealthDay? Find(IReadOnlyList<HealthDay> days, string key)
    {
        for (var index = days.Count - 1; index >= 0; index--)
        {
            var compare = string.CompareOrdinal(days[index].Date, key);
            if (compare == 0)
            {
                return days[index];
            }

            if (compare < 0)
            {
                return null;
            }
        }

        return null;
    }

    public static int Fill(IReadOnlyList<HealthDay> days, HealthMetric metric, double strideYalms, DateTime today,
        Span<double> values, Span<bool> recorded)
    {
        var count = Math.Min(values.Length, recorded.Length);
        var filled = 0;
        for (var slot = 0; slot < count; slot++)
        {
            var day = Find(days, DateKey(today.Date.AddDays(slot - (count - 1))));
            recorded[slot] = day is not null;
            values[slot] = day is null ? 0d : DayValue(day, metric, strideYalms);
            if (day is not null)
            {
                filled++;
            }
        }

        return filled;
    }

    public static double Average(ReadOnlySpan<double> values, ReadOnlySpan<bool> recorded)
    {
        var sum = 0d;
        var count = 0;
        for (var index = 0; index < values.Length && index < recorded.Length; index++)
        {
            if (!recorded[index])
            {
                continue;
            }

            sum += values[index];
            count++;
        }

        return count == 0 ? 0d : sum / count;
    }

    public static int BestIndex(ReadOnlySpan<double> values, ReadOnlySpan<bool> recorded)
    {
        var best = -1;
        for (var index = 0; index < values.Length && index < recorded.Length; index++)
        {
            if (!recorded[index] || values[index] <= 0d)
            {
                continue;
            }

            if (best < 0 || values[index] >= values[best])
            {
                best = index;
            }
        }

        return best;
    }

    public static double Max(ReadOnlySpan<double> values)
    {
        var max = 0d;
        for (var index = 0; index < values.Length; index++)
        {
            max = Math.Max(max, values[index]);
        }

        return max;
    }

    public static HealthTrend Trend(IReadOnlyList<HealthDay> days, HealthMetric metric, double strideYalms,
        DateTime today)
    {
        Span<double> values = stackalloc double[WeekDays * 2];
        Span<bool> recorded = stackalloc bool[WeekDays * 2];
        Fill(days, metric, strideYalms, today, values, recorded);
        var previousRecorded = Count(recorded[..WeekDays]);
        var recentRecorded = Count(recorded[WeekDays..]);
        var previous = Average(values[..WeekDays], recorded[..WeekDays]);
        var recent = Average(values[WeekDays..], recorded[WeekDays..]);
        var available = previousRecorded >= MinTrendDays && recentRecorded >= MinTrendDays &&
                        (previous > 0d || recent > 0d);
        return new HealthTrend(available, recent, previous, Direction(recent, previous));
    }

    public static TrendDirection Direction(double recent, double previous)
    {
        var reference = Math.Max(Math.Abs(previous), Math.Abs(recent));
        if (reference <= 0d || Math.Abs(recent - previous) <= reference * SteadyFraction)
        {
            return TrendDirection.Steady;
        }

        return recent > previous ? TrendDirection.Up : TrendDirection.Down;
    }

    private static int Count(ReadOnlySpan<bool> flags)
    {
        var count = 0;
        for (var index = 0; index < flags.Length; index++)
        {
            if (flags[index])
            {
                count++;
            }
        }

        return count;
    }
}
