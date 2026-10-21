using Aetherphone.Core.Venues;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueHoursTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.CreateCustomTimeZone("test+2", TimeSpan.FromHours(2),
        "test+2", "test+2");

    private static VenueOpening Opening(double startHours, double durationHours) =>
        new(Now.AddHours(startHours), Now.AddHours(startHours + durationHours));

    [Fact]
    public void Collect_GroupsOpeningsByLocalDay()
    {
        var slots = new List<VenueHoursSlot>();
        VenueHours.Collect([Opening(2, 3), Opening(26, 3), Opening(5 * 24, 2)], Now, Berlin, slots);

        Assert.Equal(3, slots.Count);
        Assert.Equal(0, slots[0].Day);
        Assert.Equal(new DateTime(2026, 10, 3, 22, 0, 0), slots[0].StartLocal);
        Assert.Equal(1, slots[1].Day);
        Assert.Equal(5, slots[2].Day);
    }

    [Fact]
    public void Collect_LateNightOpeningLandsOnTheNextLocalDay()
    {
        var slots = new List<VenueHoursSlot>();
        VenueHours.Collect([Opening(4.5, 2)], Now, Berlin, slots);

        var slot = Assert.Single(slots);
        Assert.Equal(1, slot.Day);
        Assert.Equal(new DateTime(2026, 10, 4, 0, 30, 0), slot.StartLocal);
    }

    [Fact]
    public void Collect_OngoingOpeningFromYesterdayCountsAsToday()
    {
        var slots = new List<VenueHoursSlot>();
        VenueHours.Collect([Opening(-23, 25)], Now, Berlin, slots);

        var slot = Assert.Single(slots);
        Assert.Equal(0, slot.Day);
        Assert.True(slot.Now);
    }

    [Fact]
    public void Collect_DropsOpeningsBeyondTheWeekAndFinishedOnes()
    {
        var slots = new List<VenueHoursSlot>();
        VenueHours.Collect([Opening(8 * 24, 2), Opening(-30, 2)], Now, Berlin, slots);

        Assert.Empty(slots);
    }

    [Fact]
    public void CollectOpenings_KeepsEveryScheduleSlotWithinAWeekSorted()
    {
        var dto = new FfxivVenueDto
        {
            Id = "a",
            Name = "Venue",
            Schedule =
            [
                Schedule(Now.AddDays(3), Now.AddDays(3).AddHours(3), false),
                Schedule(Now.AddHours(-1), Now.AddHours(2), true),
                Schedule(Now.AddDays(9), Now.AddDays(9).AddHours(3), false),
            ],
            ScheduleOverrides =
            [
                new FfxivOverrideDto { Open = true, Start = Now.AddDays(1), End = Now.AddDays(1).AddHours(2) },
                new FfxivOverrideDto { Open = false, Start = Now.AddDays(2), End = Now.AddDays(2).AddHours(2) },
            ],
        };

        var openings = VenueMapper.CollectOpenings(dto, Now);

        Assert.Equal(3, openings.Count);
        Assert.Equal(Now.AddHours(-1), openings[0].StartUtc);
        Assert.Equal(Now.AddDays(1), openings[1].StartUtc);
        Assert.Equal(Now.AddDays(3), openings[2].StartUtc);
    }

    [Fact]
    public void CollectOpenings_NoScheduleReturnsEmpty()
    {
        Assert.Empty(VenueMapper.CollectOpenings(new FfxivVenueDto { Id = "a", Name = "Venue" }, Now));
    }

    private static FfxivScheduleDto Schedule(DateTime start, DateTime end, bool isNow) =>
        new()
        {
            Resolution = new FfxivResolutionDto
            {
                Start = new DateTimeOffset(start), End = new DateTimeOffset(end), IsNow = isNow,
            },
        };
}
