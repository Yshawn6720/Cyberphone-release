using System.Collections.Generic;
using Aetherphone.Core.GameChat;
using Aetherphone.Windows;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PopoutTabsTests
{
    [Fact]
    public void AnOldSingleKeyStateBecomesAOneTabGroup()
    {
        var state = new LinkpearlPopoutState { Key = "tell:hydaelyn@omega" };

        Assert.True(PopoutTabs.Migrate(state));
        Assert.Equal(new[] { "tell:hydaelyn@omega" }, state.Keys);
        Assert.Equal(0, state.Active);
    }

    [Fact]
    public void AStateThatAlreadyCarriesTabsKeepsThem()
    {
        var state = new LinkpearlPopoutState
        {
            Key = "tab:alpha",
            Keys = { "tab:alpha", "tab:beta" },
            Active = 1,
        };

        Assert.True(PopoutTabs.Migrate(state));
        Assert.Equal(new[] { "tab:alpha", "tab:beta" }, state.Keys);
        Assert.Equal(1, state.Active);
    }

    [Fact]
    public void AnEmptyStateRestoresNothing()
    {
        var state = new LinkpearlPopoutState();

        Assert.False(PopoutTabs.Migrate(state));
        Assert.Empty(state.Keys);
    }

    [Fact]
    public void MigrationDropsBlanksAndDuplicatesAndClampsTheActiveIndex()
    {
        var state = new LinkpearlPopoutState
        {
            Keys = { "tab:alpha", string.Empty, "tab:alpha", "tab:beta" },
            Active = 7,
        };

        Assert.True(PopoutTabs.Migrate(state));
        Assert.Equal(new[] { "tab:alpha", "tab:beta" }, state.Keys);
        Assert.Equal(1, state.Active);
    }

    [Fact]
    public void MigrationTrimsAGroupBiggerThanTheTabCap()
    {
        var state = new LinkpearlPopoutState { Active = 6 };
        for (var index = 0; index < PopoutTabs.MaxTabs + 3; index++)
        {
            state.Keys.Add("tab:" + index);
        }

        Assert.True(PopoutTabs.Migrate(state));
        Assert.Equal(PopoutTabs.MaxTabs, state.Keys.Count);
        Assert.Equal(PopoutTabs.MaxTabs - 1, state.Active);
    }

    [Fact]
    public void AddingStopsAtTheTabCapAndIgnoresConversationsAlreadyHeld()
    {
        var keys = new List<string>();
        for (var index = 0; index < PopoutTabs.MaxTabs; index++)
        {
            Assert.True(PopoutTabs.Add(keys, "tab:" + index));
        }

        Assert.False(PopoutTabs.Add(keys, "tab:overflow"));
        Assert.False(PopoutTabs.Add(keys, "tab:0"));
        Assert.False(PopoutTabs.Add(keys, string.Empty));
        Assert.Equal(PopoutTabs.MaxTabs, keys.Count);
    }

    [Fact]
    public void ClosingATabBeforeTheActiveOneKeepsTheSameConversationActive()
    {
        var keys = new List<string> { "tab:alpha", "tab:beta", "tab:gamma" };

        var active = PopoutTabs.Remove(keys, 2, 0);

        Assert.Equal(1, active);
        Assert.Equal("tab:gamma", keys[active]);
    }

    [Fact]
    public void ClosingTheActiveTabFallsBackToTheOneBesideIt()
    {
        var keys = new List<string> { "tab:alpha", "tab:beta", "tab:gamma" };

        var active = PopoutTabs.Remove(keys, 2, 2);

        Assert.Equal(1, active);
        Assert.Equal("tab:beta", keys[active]);
    }

    [Fact]
    public void ClosingTheActiveTabInTheMiddleKeepsTheIndex()
    {
        var keys = new List<string> { "tab:alpha", "tab:beta", "tab:gamma" };

        var active = PopoutTabs.Remove(keys, 1, 1);

        Assert.Equal(1, active);
        Assert.Equal("tab:gamma", keys[active]);
    }

    [Fact]
    public void ClosingTheLastTabLeavesAnEmptyGroup()
    {
        var keys = new List<string> { "tab:alpha" };

        var active = PopoutTabs.Remove(keys, 0, 0);

        Assert.Equal(0, active);
        Assert.Empty(keys);
    }

    [Fact]
    public void ANewConversationLandsInTheLeastRecentlyActiveWindowWithRoom()
    {
        var tabCounts = new[] { 2, 1, 0, PopoutTabs.MaxTabs, 3, 0 };
        var lastActive = new long[] { 500L, 900L, 0L, 100L, 200L, 0L };

        Assert.Equal(4, PopoutTabs.LeastRecentlyActive(tabCounts, lastActive));
    }

    [Fact]
    public void AFullPoolOfFullWindowsHasNowhereToPutANewConversation()
    {
        var tabCounts = new int[LinkpearlPopouts.MaxWindows];
        var lastActive = new long[LinkpearlPopouts.MaxWindows];
        for (var index = 0; index < tabCounts.Length; index++)
        {
            tabCounts[index] = PopoutTabs.MaxTabs;
            lastActive[index] = index;
        }

        Assert.Equal(-1, PopoutTabs.LeastRecentlyActive(tabCounts, lastActive));
    }

    [Fact]
    public void AnUnboundWindowIsNeverTheLeastRecentlyActiveTarget()
    {
        var tabCounts = new[] { 0, 0, 1 };
        var lastActive = new[] { 1L, 2L, 3L };

        Assert.Equal(2, PopoutTabs.LeastRecentlyActive(tabCounts, lastActive));
    }
}
