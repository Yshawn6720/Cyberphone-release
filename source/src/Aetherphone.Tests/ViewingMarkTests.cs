using Aetherphone.Core.Message;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ViewingMarkTests
{
    private static readonly DateTime Start = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CoversTheNotedKeyInsideTheGrace()
    {
        var mark = new ViewingMark();
        mark.Note("thread-a", Start);

        Assert.True(mark.Covers("thread-a", Start + ViewingMark.Grace - TimeSpan.FromMilliseconds(1)));
        Assert.False(mark.Covers("thread-b", Start));
    }

    [Fact]
    public void ExpiresOnceDrawingStops()
    {
        var mark = new ViewingMark();
        mark.Note("thread-a", Start);

        Assert.False(mark.Covers("thread-a", Start + ViewingMark.Grace));
    }

    [Fact]
    public void RenotingExtendsTheWindow()
    {
        var mark = new ViewingMark();
        mark.Note("thread-a", Start);
        mark.Note("thread-a", Start + TimeSpan.FromSeconds(3));

        Assert.True(mark.Covers("thread-a", Start + TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void ClearStopsCoveringImmediately()
    {
        var mark = new ViewingMark();
        mark.Note("thread-a", Start);
        mark.Clear();

        Assert.False(mark.Covers("thread-a", Start));
    }

    [Fact]
    public void NotingAnotherKeyReplacesTheFirst()
    {
        var mark = new ViewingMark();
        mark.Note("thread-a", Start);
        mark.Note("thread-b", Start);

        Assert.False(mark.Covers("thread-a", Start));
        Assert.True(mark.Covers("thread-b", Start));
    }
}
