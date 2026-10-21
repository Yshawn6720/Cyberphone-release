using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHistoryTests
{
    [Fact]
    public void RoutesMatchTheBackendRoundsEndpoint()
    {
        Assert.Equal("/casino/rounds", CasinoClient.RoundsPath);
        Assert.Equal("/casino/rounds", CasinoClient.RoundsPagePath(null));
        Assert.Equal("/casino/rounds", CasinoClient.RoundsPagePath(string.Empty));
    }

    [Fact]
    public void TheCursorIsEscapedForTheQueryString()
    {
        const string cursor = "2026-08-09T01:02:03.0000000Z|round9";
        Assert.Equal("/casino/rounds?cursor=2026-08-09T01%3A02%3A03.0000000Z%7Cround9",
            CasinoClient.RoundsPagePath(cursor));
    }

    [Fact]
    public void RoundStatesMatchTheBackendConstants()
    {
        Assert.Equal(0, CasinoRoundStates.Open);
        Assert.Equal(1, CasinoRoundStates.Settled);
        Assert.Equal(2, CasinoRoundStates.Voided);
    }

    [Fact]
    public void BackendHistoryPageDeserializes()
    {
        const string json = "{\"items\":[{\"roundId\":\"round2\",\"gameKind\":\"casino.slots\","
            + "\"stake\":5,\"payout\":10,\"state\":1,\"createdAtUnix\":1754700000,"
            + "\"settledAtUnix\":1754700003,\"seedCommitHash\":\"abcd\",\"revealed\":true},"
            + "{\"roundId\":\"round1\",\"gameKind\":\"casino.bartender\",\"stake\":20,\"payout\":0,"
            + "\"state\":0,\"createdAtUnix\":1754690000,\"settledAtUnix\":null,"
            + "\"seedCommitHash\":\"ef01\",\"revealed\":false}],"
            + "\"nextCursor\":\"2026-08-08T22:33:20.0000000Z|round1\"}";
        var page = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoundHistoryPage);
        Assert.NotNull(page);
        Assert.NotNull(page.Items);
        Assert.Equal(2, page.Items.Length);
        Assert.Equal("round2", page.Items[0].RoundId);
        Assert.Equal("casino.slots", page.Items[0].GameKind);
        Assert.Equal(5, page.Items[0].Stake);
        Assert.Equal(10, page.Items[0].Payout);
        Assert.Equal(CasinoRoundStates.Settled, page.Items[0].State);
        Assert.Equal(1754700003L, page.Items[0].SettledAtUnix);
        Assert.True(page.Items[0].Revealed);
        Assert.Equal(CasinoRoundStates.Open, page.Items[1].State);
        Assert.Null(page.Items[1].SettledAtUnix);
        Assert.False(page.Items[1].Revealed);
        Assert.Equal("2026-08-08T22:33:20.0000000Z|round1", page.NextCursor);
    }

    [Fact]
    public void TheLastPageCarriesANullCursor()
    {
        const string json = "{\"items\":[],\"nextCursor\":null}";
        var page = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoundHistoryPage);
        Assert.NotNull(page);
        Assert.NotNull(page.Items);
        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void MergePageAppendsTheOlderPageAfterTheCurrentRounds()
    {
        var current = new[] { Round("round4", 400), Round("round3", 300) };
        var incoming = new[] { Round("round2", 200), Round("round1", 100) };

        var merged = CasinoHistoryStore.MergePage(current, incoming);

        Assert.Equal(4, merged.Length);
        Assert.Equal("round4", merged[0].RoundId);
        Assert.Equal("round3", merged[1].RoundId);
        Assert.Equal("round2", merged[2].RoundId);
        Assert.Equal("round1", merged[3].RoundId);
    }

    [Fact]
    public void MergePageDropsRoundsTheListAlreadyHolds()
    {
        var current = new[] { Round("round3", 300), Round("round2", 200) };
        var incoming = new[] { Round("round2", 200), Round("round1", 100) };

        var merged = CasinoHistoryStore.MergePage(current, incoming);

        Assert.Equal(3, merged.Length);
        Assert.Equal("round3", merged[0].RoundId);
        Assert.Equal("round2", merged[1].RoundId);
        Assert.Equal("round1", merged[2].RoundId);
    }

    [Fact]
    public void MergePageIntoAnEmptyListReturnsTheIncomingPage()
    {
        var incoming = new[] { Round("round1", 100) };
        var merged = CasinoHistoryStore.MergePage(Array.Empty<CasinoRoundHistoryDto>(), incoming);
        Assert.Same(incoming, merged);
    }

    [Fact]
    public void MergePageOfOnlyDuplicatesKeepsTheCurrentArray()
    {
        var current = new[] { Round("round2", 200), Round("round1", 100) };
        var incoming = new[] { Round("round1", 100) };
        var merged = CasinoHistoryStore.MergePage(current, incoming);
        Assert.Same(current, merged);
    }

    [Fact]
    public void DayGroupingSplitsAtLocalMidnight()
    {
        var beforeMidnight = new DateTimeOffset(new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Local));
        var afterMidnight = new DateTimeOffset(new DateTime(2026, 8, 9, 0, 1, 0, DateTimeKind.Local));
        var sameEvening = new DateTimeOffset(new DateTime(2026, 8, 8, 20, 15, 0, DateTimeKind.Local));

        Assert.False(TimeText.SameLocalDay(beforeMidnight.ToUnixTimeSeconds(),
            afterMidnight.ToUnixTimeSeconds()));
        Assert.True(TimeText.SameLocalDay(beforeMidnight.ToUnixTimeSeconds(),
            sameEvening.ToUnixTimeSeconds()));
    }

    [Fact]
    public void TheDetailsBlobCarriesTheWholeFairnessRecipe()
    {
        var verified = new VerifiedCasinoRound(new CasinoRoundVerifyDto(
            Granted: true,
            RoundId: "round1",
            GameKind: "casino.slots",
            State: CasinoRoundStates.Settled,
            Stake: 5,
            Payout: 10,
            SeedCommitHash: "commit",
            SeedRevealed: "seed",
            NextSeedHash: "next",
            DrawLog: "s0r0:1",
            StreamBinding: "room-7#4"), CasinoRoundVerdict.Match);

        var blob = Aetherphone.Apps.Casino.CasinoApp.BuildRoundDetailsBlob(verified);

        Assert.Equal("roundId=round1\ngameKind=casino.slots\nstate=1\nstake=5\npayout=10\n"
            + "seedCommitHash=commit\nseedRevealed=seed\nnextSeedHash=next\nstreamBinding=room-7#4\n"
            + "drawLog=s0r0:1\nverdict=match", blob);
    }

    [Fact]
    public void AFailedPageHoldsTheStoreOffInsteadOfRefiringEveryFrame()
    {
        const long attemptedAtTick = 1_000_000;

        Assert.True(CasinoHistoryStore.CoolingDown(attemptedAtTick, attemptedAtTick));
        Assert.True(CasinoHistoryStore.CoolingDown(attemptedAtTick, attemptedAtTick + 16));
        Assert.True(CasinoHistoryStore.CoolingDown(attemptedAtTick, attemptedAtTick + 29_999));
        Assert.False(CasinoHistoryStore.CoolingDown(attemptedAtTick, attemptedAtTick + 30_000));
    }

    [Fact]
    public void AStoreThatHasNeverAttemptedAFetchIsNotCoolingDown()
    {
        Assert.False(CasinoHistoryStore.CoolingDown(0, 1_000_000));
    }

    private static CasinoRoundHistoryDto Round(string roundId, long createdAtUnix)
    {
        return new CasinoRoundHistoryDto(
            RoundId: roundId,
            GameKind: "casino.slots",
            Stake: 5,
            Payout: 0,
            State: CasinoRoundStates.Settled,
            CreatedAtUnix: createdAtUnix,
            SettledAtUnix: createdAtUnix + 2,
            SeedCommitHash: "hash",
            Revealed: true);
    }
}
