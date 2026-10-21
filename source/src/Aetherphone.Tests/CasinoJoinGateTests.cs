using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoJoinGateTests
{
    [Fact]
    public void SittingDuringAHandArmsTheWaitAndSittingBetweenHandsDoesNot()
    {
        Assert.True(CasinoJoinGate.ArmsWait(CasinoRoomPhases.Locked));
        Assert.True(CasinoJoinGate.ArmsWait(CasinoRoomPhases.Result));
        Assert.False(CasinoJoinGate.ArmsWait(CasinoRoomPhases.Open));
    }

    [Fact]
    public void OnlyTheBettingWindowClearsTheWait()
    {
        Assert.True(CasinoJoinGate.ClearsWait(CasinoRoomPhases.Open));
        Assert.False(CasinoJoinGate.ClearsWait(CasinoRoomPhases.Locked));
        Assert.False(CasinoJoinGate.ClearsWait(CasinoRoomPhases.Result));
    }

    [Fact]
    public void TheServerCanArmTheWaitEvenWhenTheClientDidNot()
    {
        Assert.True(CasinoJoinGate.Waiting(true, false, true));
        Assert.True(CasinoJoinGate.Waiting(true, true, false));
        Assert.False(CasinoJoinGate.Waiting(true, false, false));
    }

    [Fact]
    public void NobodyWaitsAtASeatTheyDoNotHave()
    {
        Assert.False(CasinoJoinGate.Waiting(false, true, true));
        Assert.False(CasinoJoinGate.DealtThisHand(false, false));
    }

    [Fact]
    public void AWaitingSeatIsNotDealtIn()
    {
        Assert.False(CasinoJoinGate.DealtThisHand(true, true));
        Assert.True(CasinoJoinGate.DealtThisHand(true, false));
    }

    [Fact]
    public void AWaitingSeatCannotBetEvenInTheBettingWindow()
    {
        Assert.False(CasinoJoinGate.CanPlaceBet(CasinoRoomPhases.Open, true, true, false, false));
        Assert.True(CasinoJoinGate.CanPlaceBet(CasinoRoomPhases.Open, true, false, false, false));
    }

    [Fact]
    public void BetsAreOnlyTakenInTheBettingWindow()
    {
        Assert.False(CasinoJoinGate.CanPlaceBet(CasinoRoomPhases.Locked, true, false, false, false));
        Assert.False(CasinoJoinGate.CanPlaceBet(CasinoRoomPhases.Result, true, false, false, false));
    }

    [Fact]
    public void DrainingAndPausingRefuseNewBetsWithoutTouchingTheOpenHand()
    {
        Assert.False(CasinoJoinGate.CanPlaceBet(CasinoRoomPhases.Open, true, false, true, false));
        Assert.False(CasinoJoinGate.CanPlaceBet(CasinoRoomPhases.Open, true, false, false, true));
        Assert.True(CasinoJoinGate.CanAct(true, false, true));
    }

    [Fact]
    public void ActingNeedsTheTurnAndASeatThatIsActuallyInTheHand()
    {
        Assert.False(CasinoJoinGate.CanAct(true, false, false));
        Assert.False(CasinoJoinGate.CanAct(true, true, true));
        Assert.False(CasinoJoinGate.CanAct(false, false, true));
        Assert.True(CasinoJoinGate.CanAct(true, false, true));
    }

    [Fact]
    public void ASeatTakenMidHandWaitsUntilTheTableComesBackRound()
    {
        var armed = CasinoJoinGate.ArmsWait(CasinoRoomPhases.Locked);
        Assert.True(CasinoJoinGate.Waiting(true, armed, false));
        Assert.False(CasinoJoinGate.CanPlaceBet(CasinoRoomPhases.Locked, true, armed, false, false));

        armed = CasinoJoinGate.ClearsWait(CasinoRoomPhases.Open) ? false : armed;
        Assert.False(CasinoJoinGate.Waiting(true, armed, false));
        Assert.True(CasinoJoinGate.CanPlaceBet(CasinoRoomPhases.Open, true, armed, false, false));
    }
}
