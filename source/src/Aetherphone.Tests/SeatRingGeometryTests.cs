using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SeatRingGeometryTests
{
    private const int Seats = 5;

    private static readonly Rect Arena = new(new Vector2(0f, 0f), new Vector2(400f, 300f));

    [Fact]
    public void MySeatIsAlwaysBottomCentreWhoeverIAm()
    {
        var center = SeatRing.EllipseCenter(Arena);
        for (var mySeat = 0; mySeat < Seats; mySeat++)
        {
            var slot = SeatRing.DisplaySlot(mySeat, Seats, mySeat);
            Assert.Equal(SeatRing.BottomDegrees, SeatRing.SlotDegrees(Seats, slot), 3);

            var seat = SeatRing.SlotCenter(Arena, Seats, slot);
            Assert.Equal(center.X, seat.X, 3);
            Assert.True(seat.Y > center.Y);
        }
    }

    [Fact]
    public void RotationIsAPermutationOfTheSlots()
    {
        for (var mySeat = 0; mySeat < Seats; mySeat++)
        {
            var seen = new bool[Seats];
            for (var seatIndex = 0; seatIndex < Seats; seatIndex++)
            {
                var slot = SeatRing.DisplaySlot(seatIndex, Seats, mySeat);
                Assert.InRange(slot, 0, Seats - 1);
                Assert.False(seen[slot]);
                seen[slot] = true;
            }
        }
    }

    [Fact]
    public void ASpectatorSeesTheSeatsInServerOrder()
    {
        for (var seatIndex = 0; seatIndex < Seats; seatIndex++)
        {
            Assert.Equal(seatIndex, SeatRing.DisplaySlot(seatIndex, Seats, -1));
        }
    }

    [Fact]
    public void SlotsRunLeftToRightAcrossTheLowerArc()
    {
        var previousX = float.NegativeInfinity;
        for (var slot = 0; slot < Seats; slot++)
        {
            var point = SeatRing.SlotCenter(Arena, Seats, slot);
            Assert.True(point.X > previousX);
            previousX = point.X;
        }
    }

    [Fact]
    public void LayoutPlacesEverySeatOnItsOwnRotatedSlot()
    {
        Span<Vector2> centers = stackalloc Vector2[Seats];
        const int mySeat = 3;
        SeatRing.Layout(Arena, Seats, mySeat, centers);
        for (var seatIndex = 0; seatIndex < Seats; seatIndex++)
        {
            var expected = SeatRing.SlotCenter(Arena, Seats, SeatRing.DisplaySlot(seatIndex, Seats, mySeat));
            Assert.Equal(expected.X, centers[seatIndex].X, 3);
            Assert.Equal(expected.Y, centers[seatIndex].Y, 3);
        }
    }

    [Fact]
    public void HitTestingFindsTheSeatUnderThePointAndNothingElse()
    {
        const int mySeat = 1;
        const float radius = 20f;
        for (var seatIndex = 0; seatIndex < Seats; seatIndex++)
        {
            var center = SeatRing.SlotCenter(Arena, Seats, SeatRing.DisplaySlot(seatIndex, Seats, mySeat));
            Assert.Equal(seatIndex, SeatRing.HitTest(Arena, Seats, mySeat, radius, center));
        }

        Assert.Equal(-1, SeatRing.HitTest(Arena, Seats, mySeat, radius, new Vector2(-500f, -500f)));
        Assert.Equal(-1, SeatRing.HitTest(Arena, Seats, mySeat, radius, SeatRing.DealerAnchor(Arena)));
    }

    [Fact]
    public void ChipsSitBetweenThePlayerAndTheCards()
    {
        var seat = SeatRing.SlotCenter(Arena, Seats, 0);
        var towards = SeatRing.EllipseCenter(Arena);
        var chips = SeatRing.Pushed(seat, towards, 20f, SeatRing.BetPushFactor);
        var cards = SeatRing.Pushed(seat, towards, 20f, SeatRing.HandPushFactor);
        Assert.True(Vector2.Distance(seat, chips) < Vector2.Distance(seat, cards));
    }

    [Fact]
    public void PushingFromTheCentreItselfStillProducesAPlace()
    {
        var point = new Vector2(10f, 10f);
        var pushed = SeatRing.Pushed(point, point, 20f, 2f);
        Assert.Equal(point.X, pushed.X, 3);
        Assert.True(pushed.Y < point.Y);
    }
}
