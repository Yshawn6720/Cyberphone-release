namespace Aetherphone.Core.Clock;

internal static class ClockReorder
{
    public static bool Move<T>(List<T> items, int from, int to)
    {
        if (from < 0 || from >= items.Count)
        {
            return false;
        }

        var target = Math.Clamp(to, 0, items.Count - 1);
        if (target == from)
        {
            return false;
        }

        var item = items[from];
        items.RemoveAt(from);
        items.Insert(target, item);
        return true;
    }
}
