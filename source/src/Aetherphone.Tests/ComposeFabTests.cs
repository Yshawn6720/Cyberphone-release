using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ComposeFabTests
{
    private static readonly Rect ListArea = new(Vector2.Zero, new Vector2(360f, 640f));

    [Fact]
    public void LockedPhoneBoxIsFlushToTheCorner()
    {
        var box = ComposeFab.ComputeBoxRect(ListArea, 26f, 1f, 0f, scrollbarInset: 0f);
        Assert.Equal(ListArea.Max.X, box.Max.X);
        Assert.Equal(ListArea.Max.Y, box.Max.Y);
    }

    [Fact]
    public void UnlockedPhoneBoxClearsTheNativeScrollbarColumn()
    {
        const float scrollbarSize = 14f;
        var box = ComposeFab.ComputeBoxRect(ListArea, 26f, 1f, 0f, scrollbarInset: scrollbarSize);
        Assert.Equal(ListArea.Max.X - scrollbarSize, box.Max.X);
        Assert.True(box.Max.X <= ListArea.Max.X - scrollbarSize,
            "The FAB must not extend into the column the native scrollbar occupies when it is showing.");
    }

    [Fact]
    public void ScrollbarInsetShiftsPositionWithoutChangingSize()
    {
        var locked = ComposeFab.ComputeBoxRect(ListArea, 26f, 1f, 0f, scrollbarInset: 0f);
        var unlocked = ComposeFab.ComputeBoxRect(ListArea, 26f, 1f, 0f, scrollbarInset: 14f);
        Assert.Equal(locked.Width, unlocked.Width);
        Assert.Equal(locked.Height, unlocked.Height);
    }
}
