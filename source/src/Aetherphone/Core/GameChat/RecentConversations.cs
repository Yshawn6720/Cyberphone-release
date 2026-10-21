namespace Aetherphone.Core.GameChat;

internal static class RecentConversations
{
    public static int Collect(IReadOnlyList<InboxRow> pinned, IReadOnlyList<InboxRow> rows, InboxRow[] destination)
    {
        var count = Merge(pinned, destination, 0);
        return Merge(rows, destination, count);
    }

    public static int NextIndex(int current, int count, bool continuing)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (!continuing || current < 0 || current >= count)
        {
            return 0;
        }

        return (current + 1) % count;
    }

    private static int Merge(IReadOnlyList<InboxRow> source, InboxRow[] destination, int count)
    {
        for (var sourceIndex = 0; sourceIndex < source.Count; sourceIndex++)
        {
            var row = source[sourceIndex];
            var slot = count;
            while (slot > 0 && destination[slot - 1].LastActivity < row.LastActivity)
            {
                slot--;
            }

            if (slot >= destination.Length)
            {
                continue;
            }

            for (var shift = Math.Min(count, destination.Length - 1); shift > slot; shift--)
            {
                destination[shift] = destination[shift - 1];
            }

            destination[slot] = row;
            if (count < destination.Length)
            {
                count++;
            }
        }

        return count;
    }
}
