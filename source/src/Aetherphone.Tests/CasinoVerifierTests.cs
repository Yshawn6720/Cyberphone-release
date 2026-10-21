using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoVerifierTests
{
    private const string Seed = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    private const string SeedCommit = "630dcd2966c4336691125448bbb25b4ff412a49c732db2c8abc1b8581bd710dd";
    private const string SlotsRoundId = "f00d4b1dfeedc0de1234567890abcdef";
    private const string SlotsLog = "s0r0:19;s0r1:22;s0r2:29;s0r3:33;s0r4:23";
    private const string SlotsLogWithJackpotRoll = SlotsLog + ";jackpot:114522061";
    private const string ScratchRoundId = "cafe0000000000000000000000000002";
    private const string ScratchLog =
        "prize:757510;w0:4;w1:6;w2:2;w3:2;w4:7;w5:2;g8:1;g7:4;g6:4;g5:4;g4:4;g3:0;g2:0;g1:0";
    private const string BarkeepRoundId = "cafe0000000000000000000000000003";
    private const string BarkeepLog = "patrons:7;a0:0;n0:0;k0.0:2;k0.1:1;a1:2;n1:2;k1.0:2;k1.1:0";
    private const string WheelRoundId = "cafe0000000000000000000000000004";
    private const string WheelLog = "segment:16";
    private const string SpinRoundId = "cafe0000000000000000000000000005";
    private const string SpinLog = "segment:6";
    private const string BingoRoundId = "cafe0000000000000000000000000006";
    private const string BingoLog =
        "card:2;card:8;card:5;card:1;card:6;card:7;card:0;card:12;card:6;card:6;card:4;card:7;card:2;card:9;"
        + "card:7;card:11;card:6;card:9;card:2;card:9;card:13;card:12;card:5;card:7;"
        + "ball:55;ball:3;ball:55;ball:57";
    private const string BingoTwoCardLog =
        "card:2;card:8;card:5;card:1;card:6;card:7;card:0;card:12;card:6;card:6;card:4;card:7;card:2;card:9;"
        + "card:7;card:11;card:6;card:9;card:2;card:9;card:13;card:12;card:5;card:7;"
        + "card:10;card:1;card:7;card:9;card:2;card:13;card:5;card:11;card:0;card:3;card:10;card:1;card:7;"
        + "card:6;card:1;card:10;card:5;card:11;card:8;card:10;card:2;card:2;card:8;card:7";
    private const string BlackjackRoundId = "cafe0000000000000000000000000007";
    private const string BlackjackStreamBinding = "table-vector#4";

    [Fact]
    public void SlotsVectorReproducesTheCommitAndEveryDraw()
    {
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, SlotsRoundId, SlotsLog));
    }

    // Rounds played since the per-chip jackpot carry one trailing roll under a hundred and fifty
    // million; rounds from before it carry none. Both shapes have to verify, because history holds
    // both and a mismatch verdict is an accusation.
    [Fact]
    public void SlotsVectorWithTheJackpotRollReplays()
    {
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, SlotsRoundId,
                SlotsLogWithJackpotRoll));
    }

    [Fact]
    public void ATamperedJackpotRollRefuses()
    {
        const string tamperedLog = SlotsLog + ";jackpot:114522062";
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, SlotsRoundId, tamperedLog));
    }

    [Fact]
    public void AJackpotRollAtOrOverItsBoundRefuses()
    {
        const string overBoundLog = SlotsLog + ";jackpot:150000000";
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, SlotsRoundId, overBoundLog));
    }

    [Fact]
    public void ScratchWinnerVectorReplays()
    {
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.ScratchKind, Seed, SeedCommit, ScratchRoundId, ScratchLog));
    }

    [Fact]
    public void BarkeepScriptVectorReplays()
    {
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.BartenderKind, Seed, SeedCommit, BarkeepRoundId, BarkeepLog));
    }

    [Fact]
    public void WheelSegmentVectorReplays()
    {
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.WheelKind, Seed, SeedCommit, WheelRoundId, WheelLog));
    }

    [Fact]
    public void DailySpinVectorReplays()
    {
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.DailySpinKind, Seed, SeedCommit, SpinRoundId, SpinLog));
    }

    [Fact]
    public void BingoCardAndBallVectorReplays()
    {
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.BingoKind, Seed, SeedCommit, BingoRoundId, BingoLog));
    }

    [Fact]
    public void ASecondCardRestartsTheColumnPoolPattern()
    {
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.BingoKind, Seed, SeedCommit, BingoRoundId, BingoTwoCardLog));
    }

    [Fact]
    public void ABingoDrawAtItsNarrowedBoundFails()
    {
        const string tampered = "card:2;card:8;card:5;card:1;card:11";
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.BingoKind, Seed, SeedCommit, BingoRoundId, tampered));
    }

    [Fact]
    public void TestLocalReferenceAgreesWithThePinnedVector()
    {
        var reference = ReferenceLog(Seed, SlotsRoundId);
        Assert.Equal(SlotsLog, reference);
    }

    // The seated table deals every seat from one shoe, so its stream is keyed to the room and hand
    // rather than to any seat's round id, and the verify answer carries that binding. These three
    // pin the whole contract: the binding replays the shuffle, the round id alone does not, and an
    // absent binding still means the round id, which is every solo game.
    [Fact]
    public void ABlackjackShoeReplaysThroughItsTableBinding()
    {
        var log = ReferenceShuffleLog(Seed, BlackjackStreamBinding);
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.BlackjackKind, Seed, SeedCommit, BlackjackRoundId, log,
                BlackjackStreamBinding));
    }

    [Fact]
    public void ABlackjackShoeKeyedToTheWrongInfoFails()
    {
        var log = ReferenceShuffleLog(Seed, BlackjackStreamBinding);
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.BlackjackKind, Seed, SeedCommit, BlackjackRoundId, log));
    }

    [Fact]
    public void AnEmptyBindingKeysTheStreamToTheRoundIdItself()
    {
        var log = ReferenceShuffleLog(Seed, BlackjackRoundId);
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.BlackjackKind, Seed, SeedCommit, BlackjackRoundId, log));
    }

    [Fact]
    public void ASettledTableDtoCarriesItsBindingIntoTheVerdict()
    {
        var settled = new CasinoRoundVerifyDto(
            Granted: true,
            RoundId: BlackjackRoundId,
            GameKind: "casino.blackjack",
            State: CasinoRoundStates.Settled,
            Stake: 250,
            Payout: 500,
            SeedCommitHash: SeedCommit,
            SeedRevealed: Seed,
            NextSeedHash: SeedCommit,
            DrawLog: ReferenceShuffleLog(Seed, BlackjackStreamBinding),
            StreamBinding: BlackjackStreamBinding);
        Assert.Equal(CasinoRoundVerdict.Match, CasinoVerifier.Verify(settled));
    }

    [Fact]
    public void ATamperedShuffleSwapFails()
    {
        var log = ReferenceShuffleLog(Seed, BlackjackStreamBinding);
        var firstValueEnd = log.IndexOf(';');
        var tampered = "shuffle:0" + log[firstValueEnd..];
        if (string.Equals(tampered, log, StringComparison.Ordinal))
        {
            tampered = "shuffle:1" + log[firstValueEnd..];
        }

        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.BlackjackKind, Seed, SeedCommit, BlackjackRoundId, tampered,
                BlackjackStreamBinding));
    }

    [Fact]
    public void TamperedSeedFailsTheCommitCheck()
    {
        var tampered = string.Concat(Seed.AsSpan(0, Seed.Length - 1), "0");
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, tampered, SeedCommit, SlotsRoundId, SlotsLog));
    }

    [Fact]
    public void TamperedSeedWithItsOwnCommitStillFailsTheDrawReplay()
    {
        var tampered = string.Concat("ff", Seed.AsSpan(2));
        var tamperedCommit = Convert.ToHexStringLower(SHA256.HashData(Convert.FromHexString(tampered)));
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, tampered, tamperedCommit, SlotsRoundId, SlotsLog));
    }

    [Fact]
    public void TheStreamIsBoundToTheRoundId()
    {
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, ScratchRoundId, SlotsLog));
    }

    [Fact]
    public void ATamperedDrawValueFails()
    {
        const string tamperedLog = "s0r0:20;s0r1:22;s0r2:29;s0r3:33;s0r4:23";
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, SlotsRoundId, tamperedLog));
    }

    [Fact]
    public void AnUnknownPurposeFailsClosed()
    {
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, SlotsRoundId, "mystery:1"));
    }

    [Fact]
    public void AValueAtOrAboveItsBoundFails()
    {
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, SlotsRoundId, "s0r0:40"));
    }

    [Fact]
    public void AMalformedSeedIsAMismatchNotACrash()
    {
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, "not-hex", SeedCommit, SlotsRoundId, SlotsLog));
    }

    [Fact]
    public void AnEmptyDrawLogFailsClosedInsteadOfPassingOnTheCommitAlone()
    {
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, SeedCommit, SlotsRoundId, string.Empty));
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsKind, Seed, "00" + SeedCommit[2..], SlotsRoundId, string.Empty));
    }

    [Fact]
    public void ASettledDtoWithoutADrawLogIsAMismatchNotAMatch()
    {
        var evidenceless = new CasinoRoundVerifyDto(
            Granted: true,
            RoundId: SlotsRoundId,
            GameKind: "casino.slots",
            State: CasinoRoundStates.Settled,
            SeedCommitHash: SeedCommit,
            SeedRevealed: Seed,
            DrawLog: "");
        Assert.Equal(CasinoRoundVerdict.Mismatch, CasinoVerifier.Verify(evidenceless));
    }

    [Fact]
    public void AnOpenRoundStaysUnrevealed()
    {
        var open = new CasinoRoundVerifyDto(
            Granted: false,
            Reason: "round_open",
            RoundId: BarkeepRoundId,
            GameKind: "casino.bartender",
            State: CasinoRoundStates.Open,
            SeedCommitHash: SeedCommit);
        Assert.Equal(CasinoRoundVerdict.Unrevealed, CasinoVerifier.Verify(open));
    }

    [Fact]
    public void ASettledRoundWithoutARevealedSeedStaysUnrevealed()
    {
        var sealedRound = new CasinoRoundVerifyDto(
            Granted: true,
            RoundId: SlotsRoundId,
            GameKind: "casino.slots",
            State: CasinoRoundStates.Settled,
            SeedCommitHash: SeedCommit,
            SeedRevealed: "");
        Assert.Equal(CasinoRoundVerdict.Unrevealed, CasinoVerifier.Verify(sealedRound));
    }

    [Fact]
    public void ASettledDtoWithTheVectorMatches()
    {
        var settled = new CasinoRoundVerifyDto(
            Granted: true,
            RoundId: SlotsRoundId,
            GameKind: "casino.slots",
            State: CasinoRoundStates.Settled,
            Stake: 5,
            Payout: 10,
            SeedCommitHash: SeedCommit,
            SeedRevealed: Seed,
            NextSeedHash: SeedCommit,
            DrawLog: SlotsLog);
        Assert.Equal(CasinoRoundVerdict.Match, CasinoVerifier.Verify(settled));
    }

    [Fact]
    public void PurposeBoundsMirrorTheEngines()
    {
        Assert.True(CasinoVerifier.TryBoundFor("s0r0", 0u, out var slotsBound));
        Assert.Equal(40u, slotsBound);
        Assert.True(CasinoVerifier.TryBoundFor("prize", 0u, out var prizeBound));
        Assert.Equal(1_000_000u, prizeBound);
        Assert.True(CasinoVerifier.TryBoundFor("w0", 0u, out var winnerBound));
        Assert.Equal(12u, winnerBound);
        Assert.True(CasinoVerifier.TryBoundFor("w5", 0u, out var lastWinnerBound));
        Assert.Equal(7u, lastWinnerBound);
        Assert.True(CasinoVerifier.TryBoundFor("g8", 0u, out var shuffleBound));
        Assert.Equal(9u, shuffleBound);
        Assert.True(CasinoVerifier.TryBoundFor("l0", 0u, out var loserBound));
        Assert.Equal(14u, loserBound);
        Assert.True(CasinoVerifier.TryBoundFor("patrons", 0u, out var patronsBound));
        Assert.Equal(16u, patronsBound);
        Assert.True(CasinoVerifier.TryBoundFor("a3", 0u, out var jitterBound));
        Assert.Equal(3u, jitterBound);
        Assert.True(CasinoVerifier.TryBoundFor("n11", 0u, out var stepCountBound));
        Assert.Equal(3u, stepCountBound);
        Assert.True(CasinoVerifier.TryBoundFor("k13.3", 0u, out var stepKindBound));
        Assert.Equal(4u, stepKindBound);
        Assert.False(CasinoVerifier.TryBoundFor("w6", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("g0", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("g9", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("l9", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("a14", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("k14.0", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("k0.4", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("s41r0", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("s0r5", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("", 0u, out _));
    }

    [Fact]
    public void TheCommunalPurposeBoundsMirrorTheirRules()
    {
        Assert.True(CasinoVerifier.TrySegmentBound(CasinoWire.WheelKind, out var wheelSegments));
        Assert.Equal(50u, wheelSegments);
        Assert.True(CasinoVerifier.TrySegmentBound(CasinoWire.DailySpinKind, out var spinSegments));
        Assert.Equal(16u, spinSegments);

        Assert.True(CasinoVerifier.TryBoundFor("segment", wheelSegments, out var segmentBound));
        Assert.Equal(50u, segmentBound);
        Assert.True(CasinoVerifier.TryBoundFor("segment", spinSegments, out var spinBound));
        Assert.Equal(16u, spinBound);

        Assert.True(CasinoVerifier.TryBoundFor("card", 0, 0u, out var firstPick));
        Assert.Equal(15u, firstPick);
        Assert.True(CasinoVerifier.TryBoundFor("card", 4, 0u, out var lastPick));
        Assert.Equal(11u, lastPick);
        Assert.True(CasinoVerifier.TryBoundFor("card", 13, 0u, out var middleColumnLastPick));
        Assert.Equal(12u, middleColumnLastPick);
        Assert.True(CasinoVerifier.TryBoundFor("card", 24, 0u, out var secondCardFirstPick));
        Assert.Equal(15u, secondCardFirstPick);

        Assert.True(CasinoVerifier.TryBoundFor("ball", 0, 0u, out var firstBall));
        Assert.Equal(75u, firstBall);
        Assert.True(CasinoVerifier.TryBoundFor("ball", 73, 0u, out var lastBall));
        Assert.Equal(2u, lastBall);

        Assert.True(CasinoVerifier.TryBoundFor("shuffle", 0, 0u, out var firstSwap));
        Assert.Equal(312u, firstSwap);
        Assert.True(CasinoVerifier.TryBoundFor("shuffle", 310, 0u, out var lastSwap));
        Assert.Equal(2u, lastSwap);
    }

    [Fact]
    public void AWheelThisClientDoesNotKnowFailsClosedRatherThanGuessing()
    {
        Assert.False(CasinoVerifier.TrySegmentBound(CasinoWire.BingoKind, out var bingoSegments));
        Assert.Equal(0u, bingoSegments);
        Assert.False(CasinoVerifier.TrySegmentBound("casino.mystery", out _));
        Assert.False(CasinoVerifier.TryBoundFor("segment", 0u, out _));

        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify("casino.mystery", Seed, SeedCommit, WheelRoundId, WheelLog));
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.DailySpinKind, Seed, SeedCommit, WheelRoundId, WheelLog));
    }

    [Fact]
    public void AShuffleRunPastItsEndFailsClosedLikeAnUnknownPurpose()
    {
        Assert.False(CasinoVerifier.TryBoundFor("ball", 74, 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("ball", 900, 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("card", -1, 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("shuffle", 311, 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("shuffle", -1, 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("shuffles", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("balls", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("cards", 0u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("segments", 50u, out _));
        Assert.False(CasinoVerifier.TryBoundFor("spins", 16u, out _));
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.BingoKind, Seed, SeedCommit, BingoRoundId, "wedge:3"));
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.DailySpinKind, Seed, SeedCommit, SpinRoundId, "segment:16"));
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.WheelKind, Seed, SeedCommit, WheelRoundId, "segment:50"));
    }

    // An independent re-derivation of the table shoe's shuffle log, mirroring the server's
    // rejection-sampled Fisher-Yates over a six-deck shoe: three hundred eleven swaps whose bounds
    // descend from three hundred twelve to two.
    private static string ReferenceShuffleLog(string seedHex, string streamKeyInfo)
    {
        var seed = Convert.FromHexString(seedHex);
        var streamKey = new HMACSHA256(seed).ComputeHash(Encoding.UTF8.GetBytes(streamKeyInfo));
        using var blockHasher = new HMACSHA256(streamKey);
        var words = new List<uint>();
        var builder = new StringBuilder();
        var readIndex = 0;
        for (var cardIndex = BlackjackRules.ShoeCards - 1; cardIndex >= 1; cardIndex--)
        {
            var bound = (uint)(cardIndex + 1);
            var limit = 0x1_0000_0000UL / bound * bound;
            uint raw;
            do
            {
                while (readIndex >= words.Count)
                {
                    var counterBytes = new byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(counterBytes, (uint)(words.Count / 8));
                    var block = blockHasher.ComputeHash(counterBytes);
                    for (var wordIndex = 0; wordIndex < block.Length; wordIndex += 4)
                    {
                        words.Add(BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(wordIndex, 4)));
                    }
                }

                raw = words[readIndex];
                readIndex++;
            }
            while (raw >= limit);

            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append("shuffle:").Append(raw % bound);
        }

        return builder.ToString();
    }

    private static string ReferenceLog(string seedHex, string roundId)
    {
        var seed = Convert.FromHexString(seedHex);
        var streamKey = new HMACSHA256(seed).ComputeHash(Encoding.UTF8.GetBytes(roundId));
        using var blockHasher = new HMACSHA256(streamKey);
        var words = new List<uint>();
        for (uint blockCounter = 0; words.Count < 32; blockCounter++)
        {
            var counterBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(counterBytes, blockCounter);
            var block = blockHasher.ComputeHash(counterBytes);
            for (var wordIndex = 0; wordIndex < block.Length; wordIndex += 4)
            {
                words.Add(BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(wordIndex, 4)));
            }
        }

        var builder = new StringBuilder();
        var readIndex = 0;
        for (var reel = 0; reel < 5; reel++)
        {
            const uint bound = 40;
            const ulong limit = 0x1_0000_0000UL / bound * bound;
            uint raw;
            do
            {
                raw = words[readIndex];
                readIndex++;
            }
            while (raw >= limit);

            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append("s0r").Append(reel).Append(':').Append(raw % bound);
        }

        return builder.ToString();
    }
}
