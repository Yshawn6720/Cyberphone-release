using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FeedLaneFailureTests
{
    private sealed class Entry : IIdentified
    {
        public Entry(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }

    private static FeedLane<Entry> NewLane()
    {
        return new FeedLane<Entry>(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
    }

    [Fact]
    public void AFreshLaneIsEmptyWithoutBeingFailed()
    {
        var lane = NewLane();

        Assert.False(lane.Failed);
        Assert.True(lane.NeverLoaded);
        Assert.Equal(AepFailure.None, lane.Failure);
    }

    [Fact]
    public void ARecordedFailureIsReadableAndKeepsItsReason()
    {
        var lane = NewLane();
        var failure = new AepFailure(AepFailureKind.Server, 503, "server_error", null, "req77", null);

        lane.RecordFailure(failure);

        Assert.True(lane.Failed);
        Assert.Equal(503, lane.Failure.StatusCode);
        Assert.Equal("req77", lane.Failure.RequestId);
    }

    [Fact]
    public void ASuccessfulRefreshClearsAPriorFailureSoTheErrorDoesNotLatch()
    {
        var lane = NewLane();
        lane.RecordFailure(AepFailure.Transport(AepFailureKind.Offline));

        lane.ApplyRefresh(new[] { new Entry("a") }, null);

        Assert.False(lane.Failed);
        Assert.False(lane.NeverLoaded);
    }

    [Fact]
    public void ASuccessfulLoadMoreAlsoClearsAPriorFailure()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new Entry("a") }, "cursor");
        lane.RecordFailure(AepFailure.Transport(AepFailureKind.Timeout));

        lane.ApplyMore(new[] { new Entry("b") }, null);

        Assert.False(lane.Failed);
    }

    [Fact]
    public void AnEmptyButSuccessfulRefreshIsNotAFailure()
    {
        var lane = NewLane();

        lane.ApplyRefresh(Array.Empty<Entry>(), null);

        Assert.False(lane.Failed);
        Assert.True(lane.NeverLoaded);
    }

    [Fact]
    public void AFailedLaneWithNoItemsIsDistinguishableFromAGenuinelyEmptyOne()
    {
        var genuinelyEmpty = NewLane();
        genuinelyEmpty.ApplyRefresh(Array.Empty<Entry>(), null);

        var failed = NewLane();
        failed.RecordFailure(AepFailure.Transport(AepFailureKind.Offline));

        Assert.True(genuinelyEmpty.NeverLoaded);
        Assert.True(failed.NeverLoaded);
        Assert.False(genuinelyEmpty.Failed);
        Assert.True(failed.Failed);
    }
}
