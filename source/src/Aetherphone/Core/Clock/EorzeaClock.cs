namespace Aetherphone.Core.Clock;

internal static class EorzeaClock
{
    public const double Rate = 144.0 / 7.0;
    public const long SecondsPerDay = 86400;
    public const long SecondsPerBell = 3600;
    public const int MinutesPerDay = 1440;
    public const int BellsPerDay = 24;

    public static TimeSpan RealDay => TimeSpan.FromSeconds(SecondsPerDay / Rate);

    public static double SecondsAt(DateTime utc) => (utc - DateTime.UnixEpoch).TotalSeconds * Rate;

    public static DateTime UtcAt(double eorzeaSeconds) =>
        DateTime.UnixEpoch.AddTicks((long)Math.Round(eorzeaSeconds / Rate * TimeSpan.TicksPerSecond));

    public static double BellOfDay(DateTime utc)
    {
        var secondOfDay = SecondsAt(utc) % SecondsPerDay;
        return secondOfDay / SecondsPerBell;
    }

    public static TimeSpan UntilNextBell(DateTime utc)
    {
        var seconds = SecondsAt(utc);
        var nextBell = (Math.Floor(seconds / SecondsPerBell) + 1d) * SecondsPerBell;
        return UtcAt(nextBell) - utc;
    }

    public static double LastOccurrence(int minuteOfDay, DateTime utc)
    {
        var target = minuteOfDay * 60d;
        var seconds = SecondsAt(utc);
        return Math.Floor((seconds - target) / SecondsPerDay) * SecondsPerDay + target;
    }

    public static DateTime NextUtc(int minuteOfDay, DateTime utc)
    {
        var last = LastOccurrence(minuteOfDay, utc);
        var candidate = UtcAt(last);
        return candidate > utc ? candidate : UtcAt(last + SecondsPerDay);
    }

    public static long DayIndex(double eorzeaSeconds) => (long)Math.Floor(eorzeaSeconds / SecondsPerDay);
}
