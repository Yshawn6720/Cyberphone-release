namespace Aetherphone.Core.News;

internal readonly record struct MaintenanceWindow(long StartUnix, long EndUnix)
{
    public bool IsEmpty => StartUnix <= 0 || EndUnix < StartUnix;

    public MaintenanceStatus StatusAt(long nowUnix)
    {
        if (IsEmpty)
        {
            return MaintenanceStatus.None;
        }

        if (nowUnix < StartUnix)
        {
            return MaintenanceStatus.Upcoming;
        }

        return nowUnix <= EndUnix ? MaintenanceStatus.Active : MaintenanceStatus.Done;
    }

    public float Progress(long nowUnix)
    {
        if (IsEmpty || EndUnix == StartUnix)
        {
            return nowUnix >= EndUnix ? 1f : 0f;
        }

        return Math.Clamp((float)(nowUnix - StartUnix) / (EndUnix - StartUnix), 0f, 1f);
    }
}

internal readonly record struct MaintenancePick(int Index, MaintenanceStatus Status)
{
    public static readonly MaintenancePick None = new(-1, MaintenanceStatus.None);

    public bool Exists => Index >= 0;
}

internal sealed class MaintenanceBoard
{
    private LodestoneNewsItem[] source = Array.Empty<LodestoneNewsItem>();
    private MaintenanceWindow[] windows = Array.Empty<MaintenanceWindow>();
    private bool[] leads = Array.Empty<bool>();

    public int Count => source.Length;

    public bool Sync(LodestoneNewsItem[] items)
    {
        if (ReferenceEquals(items, source))
        {
            return false;
        }

        source = items;
        if (windows.Length != items.Length)
        {
            windows = new MaintenanceWindow[items.Length];
            leads = new bool[items.Length];
        }

        for (var index = 0; index < items.Length; index++)
        {
            var leader = LeaderOf(items, index);
            windows[index] = OwnWindow(items[leader]);
            leads[index] = leader == index;
        }

        return true;
    }

    public MaintenanceWindow WindowOf(int index) =>
        index >= 0 && index < windows.Length ? windows[index] : default;

    public MaintenanceStatus StatusOf(int index, long nowUnix) => WindowOf(index).StatusAt(nowUnix);

    public MaintenancePick Pick(long nowUnix)
    {
        var active = -1;
        var upcoming = -1;
        var finished = -1;
        for (var index = 0; index < windows.Length; index++)
        {
            if (!leads[index])
            {
                continue;
            }

            var window = windows[index];
            switch (window.StatusAt(nowUnix))
            {
                case MaintenanceStatus.Active when active < 0 || window.StartUnix > windows[active].StartUnix:
                    active = index;
                    break;
                case MaintenanceStatus.Upcoming when upcoming < 0 || window.StartUnix < windows[upcoming].StartUnix:
                    upcoming = index;
                    break;
                case MaintenanceStatus.Done when finished < 0 || window.EndUnix > windows[finished].EndUnix:
                    finished = index;
                    break;
            }
        }

        if (active >= 0)
        {
            return new MaintenancePick(active, MaintenanceStatus.Active);
        }

        if (upcoming >= 0)
        {
            return new MaintenancePick(upcoming, MaintenanceStatus.Upcoming);
        }

        return finished >= 0 ? new MaintenancePick(finished, MaintenanceStatus.Done) : MaintenancePick.None;
    }

    // Follow-up posts repeat the start and the title of the window they amend and carry the real end time, so
    // the most recently published of those posts speaks for the whole window. Separate services often share a
    // start time with different ends, which is why the titles must match too.
    private static int LeaderOf(LodestoneNewsItem[] items, int index)
    {
        var own = OwnWindow(items[index]);
        if (own.IsEmpty)
        {
            return index;
        }

        var leader = index;
        for (var candidate = 0; candidate < items.Length; candidate++)
        {
            if (candidate == leader)
            {
                continue;
            }

            var window = OwnWindow(items[candidate]);
            if (window.IsEmpty || window.StartUnix != own.StartUnix ||
                !SameAnnouncement(items[index].Title, items[candidate].Title))
            {
                continue;
            }

            var newer = items[candidate].Time > items[leader].Time;
            var earlierTie = items[candidate].Time == items[leader].Time && candidate < leader;
            if (newer || earlierTie)
            {
                leader = candidate;
            }
        }

        return leader;
    }

    private static bool SameAnnouncement(string title, string otherTitle) =>
        title.Contains(otherTitle, StringComparison.Ordinal) || otherTitle.Contains(title, StringComparison.Ordinal);

    private static MaintenanceWindow OwnWindow(LodestoneNewsItem item)
    {
        if (item.Start is not { } start || item.End is not { } end)
        {
            return default;
        }

        return new MaintenanceWindow(start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());
    }
}
