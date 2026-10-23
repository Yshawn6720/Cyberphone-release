using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VideoEngineSharingTests
{
    private static readonly TimeSpan Spacing = TimeSpan.FromSeconds(3);
    private static readonly DateTime Start = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LoadsFromTwoScreensAtOnceWaitTheirTurn()
    {
        var next = DateTime.MinValue;
        var reservedA = DateTime.MinValue;
        var reservedB = DateTime.MinValue;

        var first = LoadSpacing.Reserve(ref next, ref reservedA, Start, Spacing);
        var second = LoadSpacing.Reserve(ref next, ref reservedB, Start.AddSeconds(1), Spacing);

        Assert.Equal(TimeSpan.Zero, first);
        Assert.Equal(TimeSpan.FromSeconds(2), second);
        Assert.Equal(Start.AddSeconds(6), next);
    }

    [Fact]
    public void RepeatedLoadsOnOneScreenShareItsWaitingTurnInsteadOfQueueingBehindEachOther()
    {
        var next = DateTime.MinValue;
        var reserved = DateTime.MinValue;
        LoadSpacing.Reserve(ref next, ref reserved, Start, Spacing);

        var second = LoadSpacing.Reserve(ref next, ref reserved, Start.AddSeconds(1), Spacing);
        var third = LoadSpacing.Reserve(ref next, ref reserved, Start.AddSeconds(1.5), Spacing);
        var fourth = LoadSpacing.Reserve(ref next, ref reserved, Start.AddSeconds(2), Spacing);
        var afterTurn = LoadSpacing.Reserve(ref next, ref reserved, Start.AddSeconds(4), Spacing);

        Assert.Equal(TimeSpan.FromSeconds(2), second);
        Assert.Equal(TimeSpan.FromSeconds(1.5), third);
        Assert.Equal(TimeSpan.FromSeconds(1), fourth);
        Assert.Equal(TimeSpan.FromSeconds(2), afterTurn);
    }

    [Fact]
    public void ALoadLongAfterTheLastOneStartsAtOnce()
    {
        var next = DateTime.MinValue;
        var reserved = DateTime.MinValue;
        LoadSpacing.Reserve(ref next, ref reserved, Start, Spacing);

        var later = LoadSpacing.Reserve(ref next, ref reserved, Start.AddSeconds(10), Spacing);

        Assert.Equal(TimeSpan.Zero, later);
        Assert.Equal(Start.AddSeconds(13), next);
    }

    [Fact]
    public void TheFartherScreenIsDrawnFirst()
    {
        Span<int> order = stackalloc int[3];

        ScreenGeometry.OrderFarToNear([1f, 4f, 9f], order);

        Assert.Equal([2, 1, 0], order.ToArray());
    }

    [Fact]
    public void ScreensAtTheSameDistanceKeepTheirOrder()
    {
        Span<int> order = stackalloc int[3];

        ScreenGeometry.OrderFarToNear([9f, 9f, 1f], order);

        Assert.Equal([0, 1, 2], order.ToArray());
    }
}
