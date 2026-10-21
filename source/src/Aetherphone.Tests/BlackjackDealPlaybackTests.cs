using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackDealPlaybackTests
{
    private const string Hand = "hand-1";
    private const string NextHand = "hand-2";
    private const int SeatSlot = 0;

    [Fact]
    public void TheOpeningDealFliesOneCardAtATimeInDealRounds()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 0));
        Assert.Equal(0f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0));

        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.StaggerSeconds * 0.5f);
        Assert.True(playback.TravelOf(SeatSlot, 0) > 0f);
        Assert.Equal(0f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0));
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.StaggerSeconds);
        Assert.True(playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0) > 0f);
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 1));
        Assert.Equal(0f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.StaggerSeconds);
        Assert.True(playback.TravelOf(SeatSlot, 1) > 0f);
        Assert.Equal(0f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.TravelSeconds + BlackjackDealChoreography.StaggerSeconds * 4f);
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 0));
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 1));
        Assert.Equal(1f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0));
        Assert.Equal(1f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 1));
    }

    [Fact]
    public void AHitMidHandStillFliesFromTheShoe()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 5f);
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21, 30 }), 0f);
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 2));
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21, 30 }),
            BlackjackDealChoreography.TravelSeconds * 0.5f);
        var travel = playback.TravelOf(SeatSlot, 2);
        Assert.True(travel > 0f);
        Assert.True(travel < 1f);
    }

    [Fact]
    public void JoiningAHandInProgressShowsTheTableAlreadyDealt()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 0));
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 1));
        Assert.Equal(1f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0));
    }

    [Fact]
    public void TheNextHandAfterAMidHandJoinStillAnimates()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        playback.Update(Board(NextHand, BlackjackPhases.Dealing, new[] { 9, -1 }, new[] { 10, 11 }), 1f);
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 0));
    }

    [Fact]
    public void TheHoleCardFlipsExactlyOnceWhenItTurnsOver()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 5f);
        Assert.False(playback.HoleRevealing());

        playback.Update(Board(Hand, BlackjackPhases.DealerPlay, new[] { 4, 20 }, new[] { 8, 21 }), 0.1f);
        Assert.True(playback.HoleRevealing());
        Assert.Equal(0f, playback.HoleReveal());

        playback.Update(Board(Hand, BlackjackPhases.DealerPlay, new[] { 4, 20 }, new[] { 8, 21 }),
            BlackjackDealChoreography.RevealSeconds);
        Assert.Equal(1f, playback.HoleReveal());
        Assert.False(playback.HoleRevealing());

        playback.Update(Board(Hand, BlackjackPhases.DealerPlay, new[] { 4, 20 }, new[] { 8, 21 }), 0.1f);
        Assert.False(playback.HoleRevealing());
    }

    [Fact]
    public void ASpectatorJoiningAfterTheRevealNeverSeesAFlip()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.DealerPlay, new[] { 4, 20 }, new[] { 8, 21 }), 0f);
        Assert.False(playback.HoleRevealing());
        Assert.Equal(1f, playback.HoleReveal());
    }

    private static BlackjackDealPlayback Primed()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.Betting, null, null), 0f);
        return playback;
    }

    private static CasinoBlackjackRoomStateDto Board(string handId, int phase, int[]? dealerCards, int[]? seatCards)
    {
        CasinoBlackjackSeatDto[]? seats = null;
        if (seatCards is not null)
        {
            seats = new[]
            {
                new CasinoBlackjackSeatDto(SeatIndex: 0, UserId: "user-0", State: BlackjackSeatStates.Seated,
                    Hands: new[] { new CasinoBlackjackHandDto(Cards: seatCards) }),
            };
        }

        return new CasinoBlackjackRoomStateDto(HandId: handId, Phase: phase, DealerCards: dealerCards, Seats: seats);
    }
}
