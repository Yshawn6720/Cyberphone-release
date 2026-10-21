using System.Text.Json;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DailySpinTests
{
    private const int SpinTurns = 5;

    [Fact]
    public void TheWheelHasSixteenSegmentsAndEveryOneOfThemPays()
    {
        Assert.Equal(16, DailySpinRules.SegmentCount);
        Assert.Equal(DailySpinRules.SegmentCount, DailySpinRules.Awards.Length);
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            Assert.True(DailySpinRules.AwardOf(segment) > 0);
        }
    }

    [Fact]
    public void TheLadderIsTheEnginesTableInTheEnginesOrder()
    {
        var engine = new long[]
        {
            5, 15, 10, 30, 5, 20, 10, 45, 5, 15, 10, 60, 5, 20, 10, 15,
        };

        Assert.Equal(engine.Length, DailySpinRules.Awards.Length);
        for (var segment = 0; segment < engine.Length; segment++)
        {
            Assert.Equal(engine[segment], DailySpinRules.AwardOf(segment));
        }
    }

    [Fact]
    public void TheTableSumsToItsPublishedExpectation()
    {
        var total = 0L;
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            total += DailySpinRules.Awards[segment];
        }

        Assert.Equal(DailySpinRules.TotalAward, total);
        Assert.Equal(280, total);
        Assert.Equal(35, total * 2 / DailySpinRules.SegmentCount);
    }

    [Fact]
    public void NoTwoNeighbouringWedgesCarryTheSameNumber()
    {
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            var next = (segment + 1) % DailySpinRules.SegmentCount;
            Assert.NotEqual(DailySpinRules.AwardOf(segment), DailySpinRules.AwardOf(next));
        }
    }

    [Fact]
    public void TheTopWedgeIsTheOnlyOneThatEarnsTheLoudCelebration()
    {
        var tops = 0;
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            if (DailySpinRules.IsTopAward(segment))
            {
                tops++;
                Assert.Equal(DailySpinRules.TopAward, DailySpinRules.AwardOf(segment));
            }
        }

        Assert.Equal(1, tops);
        Assert.Equal(60, DailySpinRules.TopAward);
        Assert.True(DailySpinRules.IsTopAward(11));
        Assert.False(DailySpinRules.IsTopAward(-1));
    }

    [Fact]
    public void ASegmentOutsideTheRimPaysNothing()
    {
        Assert.False(DailySpinRules.IsSegment(-1));
        Assert.False(DailySpinRules.IsSegment(DailySpinRules.SegmentCount));
        Assert.Equal(0, DailySpinRules.AwardOf(-1));
        Assert.Equal(0, DailySpinRules.AwardOf(DailySpinRules.SegmentCount));
    }

    [Fact]
    public void ACardThatHasNotHeardBackYetPromisesNothingEitherWay()
    {
        Assert.Equal(DailySpinClaim.Unknown, DailySpinStatus.Of(null));
        Assert.False(DailySpinStatus.OffersWheel(DailySpinClaim.Unknown));
        Assert.False(DailySpinStatus.ShowsReset(DailySpinClaim.Unknown));
        Assert.False(DailySpinStatus.CanClaim(null, true));
    }

    [Fact]
    public void AnUnansweredCardStillLetsThePlayerAskTheServer()
    {
        Assert.True(DailySpinStatus.CanClaim(null, false));
        Assert.False(DailySpinStatus.CanClaim(null, true));
    }

    [Fact]
    public void AnOpenDayFromTheStatusReadOffersTheWheel()
    {
        var open = new CasinoDailySpinDto(
            Granted: false,
            RoundId: "spin-9",
            Segment: -1,
            Balance: 1_240,
            NextSpinAtUnix: 1_770_000_000,
            Claimed: false);

        Assert.Equal(DailySpinClaim.Available, DailySpinStatus.Of(open));
        Assert.True(DailySpinStatus.OffersWheel(DailySpinClaim.Available));
        Assert.True(DailySpinStatus.CanClaim(open, false));
        Assert.False(DailySpinStatus.ShowsReset(DailySpinClaim.Available));
    }

    [Fact]
    public void ASpentDayFromTheStatusReadClosesTheCardOnItsFlagAlone()
    {
        var spent = new CasinoDailySpinDto(
            Granted: false,
            Reason: CasinoReasons.AlreadyClaimed,
            RoundId: "spin-9",
            Segment: 3,
            SegmentAward: 30,
            Amount: 30,
            Balance: 1_270,
            NextSpinAtUnix: 1_770_000_000,
            Claimed: true);

        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(spent));
        Assert.False(DailySpinStatus.OffersWheel(DailySpinClaim.Claimed));
        Assert.False(DailySpinStatus.CanClaim(spent, false));
        Assert.True(DailySpinStatus.ShowsReset(DailySpinClaim.Claimed));
        Assert.Equal(30, DailySpinStatus.AwardOf(spent));

        var flagOnly = spent with { Reason = string.Empty };
        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(flagOnly));
        Assert.False(DailySpinStatus.CanClaim(flagOnly, false));
    }

    [Fact]
    public void AGrantedSpinReadsAsClaimedAndShowsItsReset()
    {
        var granted = new CasinoDailySpinDto(
            Granted: true,
            RoundId: "r9",
            Segment: 11,
            SegmentAward: 60,
            Amount: 60,
            Balance: 1_240,
            NextSpinAtUnix: 1_770_000_000);

        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(granted));
        Assert.False(DailySpinStatus.CanClaim(granted, false));
        Assert.True(DailySpinStatus.ShowsReset(DailySpinClaim.Claimed));
        Assert.Equal(60, DailySpinStatus.AwardOf(granted));
    }

    [Fact]
    public void AReplayReadsAsClaimedAndKeepsTheAmountTheLedgerPaid()
    {
        var replay = new CasinoDailySpinDto(
            Granted: false,
            Reason: CasinoReasons.AlreadyClaimed,
            RoundId: "r9",
            Segment: 3,
            SegmentAward: 30,
            Amount: 30,
            Balance: 1_240,
            NextSpinAtUnix: 1_770_000_000);

        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(replay));
        Assert.False(DailySpinStatus.CanClaim(replay, false));
        Assert.Equal(30, DailySpinStatus.AwardOf(replay));
    }

    [Fact]
    public void EveryDenialTheServerCanSendHasWarmWordsAndLeavesTheTurnOpen()
    {
        var reasons = new[]
        {
            CasinoReasons.Frozen,
            CasinoReasons.Paused,
            CasinoReasons.DailyCap,
            CasinoReasons.RuleCap,
            CasinoReasons.Unavailable,
        };

        for (var index = 0; index < reasons.Length; index++)
        {
            var denial = new CasinoDailySpinDto(Granted: false, Reason: reasons[index], Segment: -1);
            Assert.Equal(DailySpinClaim.Denied, DailySpinStatus.Of(denial));
            Assert.True(DailySpinStatus.CanClaim(denial, false));
            Assert.False(DailySpinStatus.CanClaim(denial, true));
            Assert.True(CasinoReasons.TryMessage(reasons[index], out var message), reasons[index]);
            Assert.NotEqual(Aetherphone.Core.Localization.L.Casino.ReasonGeneric.Key, message.Key);
        }

        Assert.Equal("already_claimed", CasinoReasons.AlreadyClaimed);
        Assert.Equal("paused", CasinoReasons.Paused);
        Assert.Equal("daily_cap", CasinoReasons.DailyCap);
        Assert.Equal("rule_cap", CasinoReasons.RuleCap);
    }

    [Fact]
    public void TheClaimedDayIsReadableWithoutTheFlag()
    {
        var granted = new CasinoDailySpinDto(Granted: true, Segment: 11, Amount: 60, Claimed: false);
        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(granted));

        var replay = new CasinoDailySpinDto(
            Granted: false,
            Reason: CasinoReasons.AlreadyClaimed,
            Segment: 11,
            Amount: 60,
            Claimed: false);
        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(replay));
    }

    [Fact]
    public void ACappedSpinShowsTheWedgeItStoppedOnAndPaysWhatTheLedgerAllowed()
    {
        var clipped = new CasinoDailySpinDto(
            Granted: true,
            Reason: CasinoReasons.CapReached,
            RoundId: "r9",
            Segment: 11,
            SegmentAward: 60,
            Amount: 15,
            Balance: 2_000,
            NextSpinAtUnix: 1_770_000_000);

        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(clipped));
        Assert.Equal(15, DailySpinStatus.AwardOf(clipped));
        Assert.Equal(60, DailySpinRules.AwardOf(clipped.Segment));
        Assert.True(CasinoReasons.TryMessage(clipped.Reason, out _));
    }

    [Fact]
    public void TheSpinRidesItsOwnEndpointAndNeedsNoSitting()
    {
        Assert.Equal("/casino/dailyspin", CasinoClient.DailySpinPath);
        Assert.Equal("casino.dailyspin", CasinoWire.DailySpinKind);
        Assert.Equal("casino.daily", CasinoLedgerRules.Daily);
    }

    [Fact]
    public void TheDayIsReadWithTheVerbThatMintsNothing()
    {
        var status = typeof(CasinoClient).GetMethod(nameof(CasinoClient.DailySpinStatusAsync));
        var claim = typeof(CasinoClient).GetMethod(nameof(CasinoClient.ClaimDailySpinAsync));

        Assert.NotNull(status);
        Assert.NotNull(claim);
    }

    [Fact]
    public void AnOpenDayDeserializesTheBackendStatusShape()
    {
        const string json = "{\"granted\":false,\"reason\":\"\",\"roundId\":\"spin-9\",\"segment\":-1,"
            + "\"segmentAward\":0,\"amount\":0,\"balance\":1240,\"nextSpinAtUnix\":1770000000,"
            + "\"seedCommitHash\":\"\",\"nextSeedHash\":\"\",\"claimed\":false}";
        var status = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoDailySpinDto);

        Assert.NotNull(status);
        Assert.False(status.Claimed);
        Assert.Equal(DailySpinClaim.Available, DailySpinStatus.Of(status));
    }

    [Fact]
    public void ASpentDayDeserializesTheBackendStatusShape()
    {
        const string json = "{\"granted\":false,\"reason\":\"already_claimed\",\"roundId\":\"spin-9\","
            + "\"segment\":3,\"segmentAward\":30,\"amount\":30,\"balance\":1270,"
            + "\"nextSpinAtUnix\":1770000000,\"seedCommitHash\":\"aa\",\"nextSeedHash\":\"bb\","
            + "\"claimed\":true}";
        var status = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoDailySpinDto);

        Assert.NotNull(status);
        Assert.True(status.Claimed);
        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(status));
        Assert.Equal(30, DailySpinStatus.AwardOf(status));
        Assert.Equal(1_770_000_000, status.NextSpinAtUnix);
    }

    [Fact]
    public void TheSpinsCoinsAreCelebratedWhileTheRestOfTheCasinoLedgerStaysQuiet()
    {
        Assert.False(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.Daily));
        Assert.True(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.BuyIn));
        Assert.True(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.CashOut));
    }

    [Fact]
    public void TheSpinAnswersWithAWalletBalanceAndNeverAChipStack()
    {
        var properties = typeof(CasinoDailySpinDto).GetProperties();
        var hasBalance = false;
        for (var index = 0; index < properties.Length; index++)
        {
            Assert.NotEqual("Stack", properties[index].Name);
            if (string.Equals(properties[index].Name, "Balance", StringComparison.Ordinal))
            {
                hasBalance = true;
            }
        }

        Assert.True(hasBalance);
    }

    [Fact]
    public void AGrantedClaimDeserializesTheBackendShape()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r9\",\"segment\":11,"
            + "\"segmentAward\":60,\"amount\":60,\"balance\":1240,\"nextSpinAtUnix\":1770000000,"
            + "\"seedCommitHash\":\"aa\",\"nextSeedHash\":\"bb\"}";
        var claim = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoDailySpinDto);

        Assert.NotNull(claim);
        Assert.True(claim.Granted);
        Assert.Equal("r9", claim.RoundId);
        Assert.Equal(11, claim.Segment);
        Assert.Equal(60, claim.SegmentAward);
        Assert.Equal(60, claim.Amount);
        Assert.Equal(1240, claim.Balance);
        Assert.Equal(1770000000, claim.NextSpinAtUnix);
        Assert.Equal("aa", claim.SeedCommitHash);
        Assert.Equal(DailySpinRules.TopAward, DailySpinRules.AwardOf(claim.Segment));
    }

    [Fact]
    public void ARefusalCarriesNoWheelAtAll()
    {
        const string json = "{\"granted\":false,\"reason\":\"frozen\",\"roundId\":\"r9\",\"segment\":-1,"
            + "\"segmentAward\":0,\"amount\":0,\"balance\":1240,\"nextSpinAtUnix\":1770000000,"
            + "\"seedCommitHash\":\"\",\"nextSeedHash\":\"\"}";
        var claim = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoDailySpinDto);

        Assert.NotNull(claim);
        Assert.False(claim.Granted);
        Assert.Equal(CasinoReasons.Frozen, claim.Reason);
        Assert.Equal(-1, claim.Segment);
        Assert.False(DailySpinRules.IsSegment(claim.Segment));
        Assert.Equal(DailySpinClaim.Denied, DailySpinStatus.Of(claim));
    }

    [Fact]
    public void TheSweepLandsTheServersSegmentUnderThePointer()
    {
        var span = WheelChoreography.SpanFor(DailySpinRules.SegmentCount);
        Assert.Equal(WheelChoreography.Tau / 16f, span, 5);

        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            var from = segment * 0.37f;
            var sweep = WheelChoreography.SweepFor(from, segment, DailySpinRules.SegmentCount, SpinTurns);
            var landed = WheelChoreography.Normalize(from + sweep);
            var rest = WheelChoreography.RestAngleOf(segment, DailySpinRules.SegmentCount);
            Assert.True(MathF.Abs(landed - rest) < 0.0005f
                || MathF.Abs(MathF.Abs(landed - rest) - WheelChoreography.Tau) < 0.0005f);
            Assert.True(sweep >= SpinTurns * WheelChoreography.Tau);
        }
    }
}
