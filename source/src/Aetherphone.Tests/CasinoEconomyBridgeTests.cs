using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoEconomyBridgeTests
{
    [Fact]
    public void EveryReasonConstantHasItsOwnMessage()
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < CasinoReasons.All.Length; index++)
        {
            var reason = CasinoReasons.All[index];
            Assert.True(CasinoReasons.TryMessage(reason, out var message),
                $"reason '{reason}' has no localized message");
            Assert.False(string.IsNullOrEmpty(message.Key));
            Assert.False(string.IsNullOrEmpty(message.Source));
            Assert.NotEqual(L.Casino.ReasonGeneric.Key, message.Key);
            Assert.True(seenKeys.Add(message.Key), $"reason '{reason}' shares a message with another reason");
        }
    }

    [Fact]
    public void UnknownReasonFallsBackToTheGenericMessage()
    {
        var message = CasinoReasons.MessageFor("some_reason_the_backend_invents_later");
        Assert.Equal(L.Casino.ReasonGeneric.Key, message.Key);
    }

    [Fact]
    public void CasinoMoneyMoveRuleIdsSkipTheEarnCelebration()
    {
        Assert.True(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.BuyIn));
        Assert.True(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.CashOut));
        Assert.True(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.Refund));
        Assert.True(CasinoLedgerRules.SkipsEarnCelebration("casino.future"));
        Assert.False(CasinoLedgerRules.SkipsEarnCelebration("coin.checkin"));
        Assert.False(CasinoLedgerRules.SkipsEarnCelebration("call.connected"));
        Assert.False(CasinoLedgerRules.SkipsEarnCelebration("casino"));
    }

    [Fact]
    public void TheDailySpinCelebratesLikeAnyOtherEarn()
    {
        Assert.False(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.Daily));

        var items = new[]
        {
            Entry("1", CasinoLedgerRules.Daily, 10),
            Entry("2", CasinoLedgerRules.CashOut, 200),
        };
        var slice = CoinStore.CollectEarnDelta(items, 210, out var celebratedDelta);
        var celebrated = Assert.Single(slice);
        Assert.Equal(CasinoLedgerRules.Daily, celebrated.RuleId);
        Assert.Equal(10, celebratedDelta);
    }

    [Fact]
    public void ForcedCashOutsNeverFireTheEarnPill()
    {
        var items = new[]
        {
            Entry("1", CasinoLedgerRules.CashOut, 120),
        };
        var slice = CoinStore.CollectEarnDelta(items, 120, out var celebratedDelta);
        Assert.Empty(slice);
        Assert.Equal(0, celebratedDelta);
    }

    [Fact]
    public void MixedLedgerCelebratesOnlyTheRealEarns()
    {
        var items = new[]
        {
            Entry("1", CasinoLedgerRules.CashOut, 200),
            Entry("2", "coin.checkin", 8),
            Entry("3", "coin.streak", 4),
        };
        var slice = CoinStore.CollectEarnDelta(items, 212, out var celebratedDelta);
        Assert.Equal(2, slice.Length);
        Assert.Equal("coin.checkin", slice[0].RuleId);
        Assert.Equal("coin.streak", slice[1].RuleId);
        Assert.Equal(12, celebratedDelta);
    }

    [Fact]
    public void PlainEarnsStillCelebrateTheFullDelta()
    {
        var items = new[]
        {
            Entry("1", "coin.checkin", 8),
            Entry("2", "game.session", 10),
            Entry("3", "coin.welcome", 100),
        };
        var slice = CoinStore.CollectEarnDelta(items, 18, out var celebratedDelta);
        Assert.Equal(2, slice.Length);
        Assert.Equal(18, celebratedDelta);
    }

    [Fact]
    public void EveryCasinoRuleIdRendersANamedLedgerRow()
    {
        Assert.NotEqual(L.Coin.RuleGeneric.Key, CoinRuleLabels.For(CasinoLedgerRules.BuyIn).Key);
        Assert.NotEqual(L.Coin.RuleGeneric.Key, CoinRuleLabels.For(CasinoLedgerRules.CashOut).Key);
        Assert.NotEqual(L.Coin.RuleGeneric.Key, CoinRuleLabels.For(CasinoLedgerRules.Refund).Key);
        Assert.NotEqual(L.Coin.RuleGeneric.Key, CoinRuleLabels.For(CasinoLedgerRules.Daily).Key);
    }

    [Fact]
    public void AMoneyMoveBalanceLandsInTheWalletWithoutARoundTrip()
    {
        var before = Wallet(1_021_206);
        var after = CoinStore.BalanceAbsorbedInto(before, 1_016_206);
        Assert.NotNull(after);
        Assert.Equal(1_016_206, after.Balance);
        Assert.Equal(before.LifetimeEarned, after.LifetimeEarned);
        Assert.Equal(before.EarnedToday, after.EarnedToday);
    }

    [Fact]
    public void AnEmptyingBuyInStillLandsAtZero()
    {
        var after = CoinStore.BalanceAbsorbedInto(Wallet(5_000), 0);
        Assert.NotNull(after);
        Assert.Equal(0, after.Balance);
    }

    [Fact]
    public void AnUnchangedBalanceLeavesTheWalletAlone()
    {
        Assert.Null(CoinStore.BalanceAbsorbedInto(Wallet(800), 800));
        Assert.Null(CoinStore.BalanceAbsorbedInto(null, 800));
    }

    private static CoinWalletDto Wallet(long balance)
    {
        return new CoinWalletDto(balance, 2_000_000, 900_000, 40, 100, 0, 3, false, false,
            string.Empty, 60, 300, Array.Empty<CoinRuleStatusDto>());
    }

    private static CoinLedgerEntryDto Entry(string id, string ruleId, long amount)
    {
        return new CoinLedgerEntryDto(id, ruleId, amount, 0, "casino", id, string.Empty, 0);
    }
}
