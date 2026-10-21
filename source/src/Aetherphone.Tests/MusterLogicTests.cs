using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Muster;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MusterLogicTests
{
    private const long Now = 1_800_000_000L;
    private const long Minute = 60L;

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 480)]
    [InlineData(0.5f, 240)]
    [InlineData(0.01f, 0)]
    [InlineData(0.02f, 15)]
    [InlineData(-3f, 0)]
    [InlineData(7f, 480)]
    public void LeadSnapsToQuarterHoursWithinEightHours(float fraction, int expected)
    {
        Assert.Equal(expected, MusterSchedule.LeadFromFraction(fraction));
    }

    [Theory]
    [InlineData(0f, 30)]
    [InlineData(1f, 480)]
    [InlineData(0.5f, 255)]
    public void DurationStaysBetweenHalfAnHourAndEightHours(float fraction, int expected)
    {
        Assert.Equal(expected, MusterSchedule.DurationFromFraction(fraction));
    }

    [Fact]
    public void NaNFractionFallsBackToTheMinimum()
    {
        Assert.Equal(0, MusterSchedule.LeadFromFraction(float.NaN));
        Assert.Equal(MusterSchedule.MinDurationMinutes, MusterSchedule.DurationFromFraction(float.NaN));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(135)]
    [InlineData(480)]
    public void LeadFractionRoundTrips(int minutes)
    {
        Assert.Equal(minutes, MusterSchedule.LeadFromFraction(MusterSchedule.LeadFraction(minutes)));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(195)]
    [InlineData(480)]
    public void DurationFractionRoundTrips(int minutes)
    {
        Assert.Equal(minutes, MusterSchedule.DurationFromFraction(MusterSchedule.DurationFraction(minutes)));
    }

    [Fact]
    public void ClampKeepsTheWireInsideTheServerLimits()
    {
        Assert.Equal(0, MusterSchedule.ClampLead(-40));
        Assert.Equal(480, MusterSchedule.ClampLead(9999));
        Assert.Equal(30, MusterSchedule.ClampDuration(5));
        Assert.Equal(480, MusterSchedule.ClampDuration(600));
        Assert.Equal(45, MusterSchedule.ClampDuration(44));
    }

    [Fact]
    public void DraftNeedsADescriptionBeforeAnythingElse()
    {
        Assert.Equal(MusterDraftIssue.NeedDescription, MusterDraft.Validate("   ", string.Empty, false, 0));
    }

    [Fact]
    public void DraftRejectsDescriptionsPastTheLimit()
    {
        var text = new string('a', MusterDraft.DescriptionMaxLength + 1);
        Assert.Equal(MusterDraftIssue.DescriptionTooLong, MusterDraft.Validate(text, "spot", true, 5));
    }

    [Fact]
    public void DraftNeedsALocationOrASpot()
    {
        Assert.Equal(MusterDraftIssue.NeedWhere, MusterDraft.Validate("Maps", "  ", false, 5));
        Assert.Equal(MusterDraftIssue.None, MusterDraft.Validate("Maps", "Aetheryte", false, 5));
        Assert.Equal(MusterDraftIssue.None, MusterDraft.Validate("Maps", string.Empty, true, 5));
    }

    [Fact]
    public void DraftNeedsAKnownDataCenter()
    {
        Assert.Equal(MusterDraftIssue.NeedDataCenter, MusterDraft.Validate("Maps", "Aetheryte", true, 0));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("  ", 0)]
    [InlineData(" hi ", 2)]
    [InlineData("a b", 3)]
    public void TrimmedLengthIgnoresOuterWhitespace(string value, int expected)
    {
        Assert.Equal(expected, MusterDraft.TrimmedLength(value));
    }

    [Fact]
    public void TallyCountsEveryStatusAndTreatsUnknownAsOnTheWay()
    {
        var attendees = new[]
        {
            Attendee("a", MusterStatuses.OnMyWay), Attendee("b", MusterStatuses.None),
            Attendee("c", MusterStatuses.RunningLate), Attendee("d", MusterStatuses.Here),
            Attendee("e", MusterStatuses.Here), Attendee("f", MusterStatuses.WhereExactly),
        };

        var tally = MusterTally.Of(attendees);

        Assert.Equal(new MusterTally(2, 1, 2, 1), tally);
        Assert.Equal(6, tally.Total);
    }

    [Fact]
    public void SectionsSplitLiveAndSoonAndDropEndedMusters()
    {
        var sections = new MusterSections();
        var directory = new[]
        {
            Muster("live", "h1", Now - 10 * Minute, Now + 50 * Minute),
            Muster("later", "h2", Now + 90 * Minute, Now + 150 * Minute),
            Muster("soon", "h3", Now + 20 * Minute, Now + 80 * Minute),
            Muster("over", "h4", Now - 90 * Minute, Now - Minute),
        };

        sections.Sync(Array.Empty<MusterDto>(), directory, Array.Empty<MusterDto>(), null, Now);

        Assert.Single(sections.Live);
        Assert.Equal("live", sections.Live[0].Id);
        Assert.Equal(new[] { "soon", "later" }, Ids(sections.Soon));
        Assert.Equal(3, sections.DiscoverCount);
        Assert.Equal(Now + MusterSections.FallbackRebuildSeconds, sections.NextRebuildUnix);
    }

    [Fact]
    public void FriendsHostsAreNotRepeatedInTheDirectory()
    {
        var sections = new MusterSections();
        var friend = Muster("friend", "pal", Now + 5 * Minute, Now + 65 * Minute);
        var directory = new[] { Muster("friend", "pal", Now + 5 * Minute, Now + 65 * Minute) };

        sections.Sync(new[] { friend }, directory, Array.Empty<MusterDto>(), null, Now);

        Assert.Single(sections.Friends);
        Assert.Empty(sections.Soon);
    }

    [Fact]
    public void MyOwnMusterStaysOutOfDiscoverAndGoing()
    {
        var sections = new MusterSections();
        var mine = Muster("mine", "me", Now + 5 * Minute, Now + 65 * Minute);
        var directory = new[] { Muster("mine", "me", Now + 5 * Minute, Now + 65 * Minute) };

        sections.Sync(Array.Empty<MusterDto>(), directory, new[] { mine }, mine, Now);

        Assert.Equal(0, sections.DiscoverCount);
        Assert.Empty(sections.Going);
    }

    [Fact]
    public void GoingIsSortedByStartAndDeduplicated()
    {
        var sections = new MusterSections();
        var going = new[]
        {
            Muster("b", "h2", Now + 60 * Minute, Now + 120 * Minute),
            Muster("a", "h1", Now - 5 * Minute, Now + 55 * Minute),
            Muster("b", "h2", Now + 60 * Minute, Now + 120 * Minute),
            Muster("gone", "h3", Now - 120 * Minute, Now - 60 * Minute),
        };

        sections.Sync(Array.Empty<MusterDto>(), Array.Empty<MusterDto>(), going, null, Now);

        Assert.Equal(new[] { "a", "b" }, Ids(sections.Going));
    }

    [Fact]
    public void SyncOnlyRebuildsWhenInputsChangeOrABoundaryPasses()
    {
        var sections = new MusterSections();
        var directory = new[] { Muster("soon", "h1", Now + 10 * Minute, Now + 70 * Minute) };
        var none = Array.Empty<MusterDto>();

        Assert.True(sections.Sync(none, directory, none, null, Now));
        Assert.False(sections.Sync(none, directory, none, null, Now + 10));
        Assert.True(sections.Sync(none, directory, none, null, Now + MusterSections.FallbackRebuildSeconds));
        Assert.Empty(sections.Live);
        Assert.True(sections.Sync(none, directory, none, null, Now + 10 * Minute));
        Assert.Single(sections.Live);
    }

    private static string[] Ids(List<MusterDto> source)
    {
        var ids = new string[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            ids[index] = source[index].Id;
        }

        return ids;
    }

    private static MusterAttendeeDto Attendee(string id, int status) =>
        new(id, "Name " + id, "World", status, Now, Now);

    private static MusterDto Muster(string id, string hostId, long startsAt, long endsAt) =>
        new(id, hostId, "Host", "World", MusterCategories.Social, "Meet", 0, 0, 0f, 0f, 0, 0, 0, 0, "Spot",
            MusterCategories.RegionEurope, 1, startsAt, endsAt, 0, 0, false, true, false, 0, 0, Now);
}
