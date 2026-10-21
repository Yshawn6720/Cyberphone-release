using Aetherphone.Core.GameChat;

namespace Aetherphone.Windows;

internal static class PopoutTabs
{
    public const int MaxTabs = 6;

    public static bool Migrate(LinkpearlPopoutState state)
    {
        if (state.Keys.Count == 0 && state.Key.Length > 0)
        {
            state.Keys.Add(state.Key);
        }

        for (var index = state.Keys.Count - 1; index >= 0; index--)
        {
            if (state.Keys[index].Length == 0 || IndexOf(state.Keys, state.Keys[index]) != index)
            {
                state.Keys.RemoveAt(index);
            }
        }

        while (state.Keys.Count > MaxTabs)
        {
            state.Keys.RemoveAt(state.Keys.Count - 1);
        }

        state.Active = state.Keys.Count == 0 ? 0 : Math.Clamp(state.Active, 0, state.Keys.Count - 1);
        return state.Keys.Count > 0;
    }

    public static int IndexOf(List<string> keys, string key)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            if (string.Equals(keys[index], key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public static bool Add(List<string> keys, string key)
    {
        if (key.Length == 0 || keys.Count >= MaxTabs || IndexOf(keys, key) >= 0)
        {
            return false;
        }

        keys.Add(key);
        return true;
    }

    public static int Remove(List<string> keys, int active, int index)
    {
        if (index < 0 || index >= keys.Count)
        {
            return active;
        }

        keys.RemoveAt(index);
        if (keys.Count == 0)
        {
            return 0;
        }

        if (index < active)
        {
            return active - 1;
        }

        return Math.Min(active, keys.Count - 1);
    }

    public static int LeastRecentlyActive(ReadOnlySpan<int> tabCounts, ReadOnlySpan<long> lastActive)
    {
        var best = -1;
        for (var index = 0; index < tabCounts.Length; index++)
        {
            if (tabCounts[index] <= 0 || tabCounts[index] >= MaxTabs)
            {
                continue;
            }

            if (best < 0 || lastActive[index] < lastActive[best])
            {
                best = index;
            }
        }

        return best;
    }
}
