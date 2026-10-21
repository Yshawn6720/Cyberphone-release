using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlayingCardsTests
{
    [Fact]
    public void EveryCardSurvivesTheRoundTripThroughRankAndSuit()
    {
        for (var card = 0; card < PlayingCards.DeckSize; card++)
        {
            var rank = PlayingCards.Rank(card);
            var suit = PlayingCards.Suit(card);
            Assert.InRange(rank, 0, PlayingCards.RankCount - 1);
            Assert.InRange(suit, 0, PlayingCards.SuitCount - 1);
            Assert.Equal(card, PlayingCards.Encode(rank, suit));
        }
    }

    [Fact]
    public void EncodingMatchesTheSolitaireAlphabetTheWireInherited()
    {
        Assert.Equal(0, PlayingCards.Encode(0, PlayingCards.Spades));
        Assert.Equal(13, PlayingCards.Encode(0, PlayingCards.Hearts));
        Assert.Equal(26, PlayingCards.Encode(0, PlayingCards.Diamonds));
        Assert.Equal(39, PlayingCards.Encode(0, PlayingCards.Clubs));
        Assert.Equal(51, PlayingCards.Encode(12, PlayingCards.Clubs));
    }

    [Fact]
    public void HeartsAndDiamondsAreTheRedSuits()
    {
        for (var card = 0; card < PlayingCards.DeckSize; card++)
        {
            var suit = PlayingCards.Suit(card);
            var red = suit == PlayingCards.Hearts || suit == PlayingCards.Diamonds;
            Assert.Equal(red, PlayingCards.IsRed(card));
        }
    }

    [Fact]
    public void OnlyTheFiftyTwoAreCards()
    {
        Assert.False(PlayingCards.IsCard(PlayingCards.FaceDown));
        Assert.False(PlayingCards.IsCard(-2));
        Assert.True(PlayingCards.IsCard(0));
        Assert.True(PlayingCards.IsCard(PlayingCards.DeckSize - 1));
        Assert.False(PlayingCards.IsCard(PlayingCards.DeckSize));
    }

    [Fact]
    public void RankLabelsReadTheWayAFaceDoes()
    {
        Assert.Equal("A", PlayingCards.RankLabel(0));
        Assert.Equal("2", PlayingCards.RankLabel(1));
        Assert.Equal("10", PlayingCards.RankLabel(9));
        Assert.Equal("J", PlayingCards.RankLabel(10));
        Assert.Equal("Q", PlayingCards.RankLabel(11));
        Assert.Equal("K", PlayingCards.RankLabel(12));
    }

    [Fact]
    public void CardGeometryKeepsItsAspectInBothDirections()
    {
        Assert.Equal(140f, PlayingCards.HeightFor(100f), 3);
        Assert.Equal(100f, PlayingCards.WidthFor(140f), 3);
        Assert.Equal(12f, PlayingCards.RoundingFor(100f), 3);
    }
}
