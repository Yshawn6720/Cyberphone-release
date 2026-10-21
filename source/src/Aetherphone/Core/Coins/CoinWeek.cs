using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Core.Coins;

internal static class CoinWeek
{
    public const int Days = 7;

    public static DateTime WindowStart(DateTime todayLocal) => todayLocal.Date.AddDays(1 - Days);

    public static bool Covered(CoinLedgerEntryDto[] entries, bool endReached, DateTime todayLocal,
        TimeZoneInfo zone)
    {
        if (endReached)
        {
            return true;
        }

        if (entries.Length == 0)
        {
            return false;
        }

        return LocalDate(entries[^1].CreatedAtUnix, zone) < WindowStart(todayLocal);
    }

    public static long Fill(CoinLedgerEntryDto[] entries, DateTime todayLocal, TimeZoneInfo zone, Span<long> days)
    {
        days.Clear();
        var start = WindowStart(todayLocal);
        long total = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (!CountsAsEarned(entry))
            {
                continue;
            }

            var offset = (int)(LocalDate(entry.CreatedAtUnix, zone) - start).TotalDays;
            if (offset < 0 || offset >= days.Length)
            {
                continue;
            }

            days[offset] += entry.Amount;
            total += entry.Amount;
        }

        return total;
    }

    public static bool CountsAsEarned(CoinLedgerEntryDto entry) =>
        entry.Amount > 0 && !CasinoLedgerRules.SkipsEarnCelebration(entry.RuleId);

    public static long Peak(ReadOnlySpan<long> days)
    {
        long peak = 0;
        for (var index = 0; index < days.Length; index++)
        {
            peak = Math.Max(peak, days[index]);
        }

        return peak;
    }

    private static DateTime LocalDate(long unixSeconds, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime, zone).Date;
}
