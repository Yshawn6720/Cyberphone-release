using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoPendingRoundTests
{
    private const ulong Character = 42;
    private const ulong OtherCharacter = 77;

    [Fact]
    public void RememberingARoundCopiesTheSnapshotAndKeepsOtherCharacters()
    {
        var snapshot = new Dictionary<ulong, PendingCasinoRound>
        {
            [OtherCharacter] = Pending("other-round"),
        };

        var next = CasinoPlayStore.RememberRound(snapshot, Character, Pending("round1"));

        Assert.NotNull(next);
        Assert.NotSame(snapshot, next);
        Assert.Equal(2, next.Count);
        Assert.Equal("round1", next[Character].RoundId);
        Assert.Equal("other-round", next[OtherCharacter].RoundId);
        Assert.False(snapshot.ContainsKey(Character));
    }

    [Fact]
    public void RememberingTheSameRoundAgainIsANoOp()
    {
        var snapshot = new Dictionary<ulong, PendingCasinoRound>
        {
            [Character] = Pending("round1"),
        };

        Assert.Null(CasinoPlayStore.RememberRound(snapshot, Character, Pending("round1")));
    }

    [Fact]
    public void TheRememberedIntentCarriesEverythingTheIdempotentReplayNeeds()
    {
        var pending = Pending("round1");
        var next = CasinoPlayStore.RememberRound(new Dictionary<ulong, PendingCasinoRound>(), Character, pending);

        Assert.NotNull(next);
        var recovered = next[Character];
        Assert.Equal("casino.slots", recovered.GameKind);
        Assert.Equal("sit1", recovered.SittingId);
        Assert.Equal("round1", recovered.RoundId);
        Assert.Equal(2, recovered.Stake);
    }

    [Fact]
    public void ClearingRemovesOnlyTheMatchingRound()
    {
        var snapshot = new Dictionary<ulong, PendingCasinoRound>
        {
            [Character] = Pending("round1"),
            [OtherCharacter] = Pending("other-round"),
        };

        var next = CasinoPlayStore.ClearRound(snapshot, Character, "round1");

        Assert.NotNull(next);
        Assert.NotSame(snapshot, next);
        Assert.False(next.ContainsKey(Character));
        Assert.Equal("other-round", next[OtherCharacter].RoundId);
        Assert.True(snapshot.ContainsKey(Character));
    }

    [Fact]
    public void ClearingADifferentRoundIdLeavesThePendingIntentAlone()
    {
        var snapshot = new Dictionary<ulong, PendingCasinoRound>
        {
            [Character] = Pending("round1"),
        };

        Assert.Null(CasinoPlayStore.ClearRound(snapshot, Character, "round2"));
        Assert.Null(CasinoPlayStore.ClearRound(snapshot, OtherCharacter, "round1"));
    }

    [Fact]
    public void AnotherTapAtTheSameTableReplaysTheStrandedRoundInsteadOfStakingAgain()
    {
        Assert.True(CasinoPlayStore.ReplaysBeforeStaking(Pending("round1"), "sit1"));
    }

    [Fact]
    public void ARoundLeftBehindByAClosedSittingNeverBlocksTheTableInPlay()
    {
        Assert.False(CasinoPlayStore.ReplaysBeforeStaking(Pending("round1"), "sit2"));
        Assert.False(CasinoPlayStore.ReplaysBeforeStaking(null, "sit1"));
        Assert.False(CasinoPlayStore.ReplaysBeforeStaking(Pending("round1"), string.Empty));
    }

    [Fact]
    public void AReplayedRoundNeverWritesAClosedSittingsStackOntoTheOneInPlay()
    {
        var inPlay = State("sit2", stack: 300);

        Assert.Null(CasinoStore.StackAbsorbedInto(inPlay, "sit1", 0));
        Assert.Equal(300, inPlay.Sitting!.Stack);
    }

    [Fact]
    public void TheSittingInPlayStillAbsorbsItsOwnRounds()
    {
        var inPlay = State("sit2", stack: 300);

        var next = CasinoStore.StackAbsorbedInto(inPlay, "sit2", 295);

        Assert.NotNull(next);
        Assert.Equal(295, next.Sitting!.Stack);
        Assert.Equal(300, inPlay.Sitting!.Stack);
    }

    [Fact]
    public void AnUnchangedOrUnseatedStackIsNotWorthANewState()
    {
        Assert.Null(CasinoStore.StackAbsorbedInto(State("sit2", stack: 300), "sit2", 300));
        Assert.Null(CasinoStore.StackAbsorbedInto(State("sit2", stack: 300), string.Empty, 295));
        Assert.Null(CasinoStore.StackAbsorbedInto(new CasinoStateDto(), "sit2", 295));
        Assert.Null(CasinoStore.StackAbsorbedInto(null, "sit2", 295));
    }

    private static CasinoStateDto State(string sittingId, long stack)
    {
        return new CasinoStateDto(
            Sitting: new CasinoSittingDto(Id: sittingId, GameKind: CasinoWire.SlotsKind, Stack: stack));
    }

    private static PendingCasinoRound Pending(string roundId)
    {
        return new PendingCasinoRound
        {
            GameKind = CasinoWire.SlotsKind,
            SittingId = "sit1",
            RoundId = roundId,
            Stake = 2,
        };
    }
}
