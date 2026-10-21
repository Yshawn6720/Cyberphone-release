using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoinWeekTests
{
    private static readonly DateTime Today = new(2026, 10, 3);

    [Fact]
    public void EarningsLandOnTheirLocalDayAndSpendingIsIgnored()
    {
        var entries = new[]
        {
            Entry("coin.checkin", 20, Today.AddHours(9)),
            Entry("game.session", 10, Today.AddHours(8)),
            Entry("purchase", -500, Today.AddHours(7)),
            Entry("chat.conversation", 8, Today.AddDays(-1).AddHours(22)),
            Entry("coin.checkin", 20, Today.AddDays(-6).AddHours(1)),
        };
        Span<long> days = stackalloc long[CoinWeek.Days];

        var total = CoinWeek.Fill(entries, Today, TimeZoneInfo.Utc, days);

        Assert.Equal(58, total);
        Assert.Equal(30, days[6]);
        Assert.Equal(8, days[5]);
        Assert.Equal(20, days[0]);
        Assert.Equal(30, CoinWeek.Peak(days));
    }

    [Fact]
    public void CasinoCashOutsAreNotCountedAsEarnings()
    {
        var entries = new[]
        {
            Entry(CasinoLedgerRules.CashOut, 400, Today.AddHours(3)),
            Entry(CasinoLedgerRules.Daily, 15, Today.AddHours(2)),
        };
        Span<long> days = stackalloc long[CoinWeek.Days];

        var total = CoinWeek.Fill(entries, Today, TimeZoneInfo.Utc, days);

        Assert.Equal(15, total);
    }

    [Fact]
    public void EntriesOlderThanTheWindowAreDropped()
    {
        var entries = new[] { Entry("coin.checkin", 20, Today.AddDays(-7).AddHours(12)) };
        Span<long> days = stackalloc long[CoinWeek.Days];

        Assert.Equal(0, CoinWeek.Fill(entries, Today, TimeZoneInfo.Utc, days));
    }

    [Fact]
    public void CoverageNeedsAnEntryOlderThanTheWindowOrTheLedgerEnd()
    {
        var recent = new[] { Entry("coin.checkin", 20, Today.AddDays(-2)) };
        var older = new[] { Entry("coin.checkin", 20, Today.AddDays(-2)), Entry("coin.checkin", 20, Today.AddDays(-9)) };

        Assert.False(CoinWeek.Covered(recent, false, Today, TimeZoneInfo.Utc));
        Assert.True(CoinWeek.Covered(recent, true, Today, TimeZoneInfo.Utc));
        Assert.True(CoinWeek.Covered(older, false, Today, TimeZoneInfo.Utc));
        Assert.False(CoinWeek.Covered(Array.Empty<CoinLedgerEntryDto>(), false, Today, TimeZoneInfo.Utc));
    }

    [Fact]
    public void SavingGoalProgressClampsAndCountsDown()
    {
        Assert.Equal(0.5f, CoinSavingGoal.Fraction(250, 500));
        Assert.Equal(1f, CoinSavingGoal.Fraction(900, 500));
        Assert.Equal(0f, CoinSavingGoal.Fraction(-20, 500));
        Assert.Equal(1f, CoinSavingGoal.Fraction(0, 0));
        Assert.Equal(250, CoinSavingGoal.Remaining(250, 500));
        Assert.Equal(0, CoinSavingGoal.Remaining(900, 500));
    }

    private static CoinLedgerEntryDto Entry(string ruleId, long amount, DateTime utc) =>
        new(Guid.NewGuid().ToString(), ruleId, amount, 0, string.Empty, string.Empty, string.Empty,
            new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds());
}
