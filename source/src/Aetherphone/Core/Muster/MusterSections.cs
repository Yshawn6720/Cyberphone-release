using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Muster;

internal sealed class MusterSections
{
    public const int FallbackRebuildSeconds = 30;

    private static readonly Comparison<MusterDto> ByStart =
        static (left, right) => left.StartsAtUnix.CompareTo(right.StartsAtUnix);

    public readonly List<MusterDto> Friends = new();
    public readonly List<MusterDto> Live = new();
    public readonly List<MusterDto> Soon = new();
    public readonly List<MusterDto> Going = new();

    private MusterDto[] lastContacts = Array.Empty<MusterDto>();
    private MusterDto[] lastDirectory = Array.Empty<MusterDto>();
    private MusterDto[] lastGoing = Array.Empty<MusterDto>();
    private MusterDto? lastMine;
    private bool built;

    public long NextRebuildUnix { get; private set; }

    public int DiscoverCount => Friends.Count + Live.Count + Soon.Count;

    public bool Sync(MusterDto[] contacts, MusterDto[] directory, MusterDto[] going, MusterDto? mine, long nowUnix)
    {
        if (built && ReferenceEquals(contacts, lastContacts) && ReferenceEquals(directory, lastDirectory)
            && ReferenceEquals(going, lastGoing) && ReferenceEquals(mine, lastMine) && nowUnix < NextRebuildUnix)
        {
            return false;
        }

        built = true;
        lastContacts = contacts;
        lastDirectory = directory;
        lastGoing = going;
        lastMine = mine;
        Rebuild(contacts, directory, going, mine, nowUnix);
        return true;
    }

    public void Invalidate() => built = false;

    private void Rebuild(MusterDto[] contacts, MusterDto[] directory, MusterDto[] going, MusterDto? mine,
        long nowUnix)
    {
        Friends.Clear();
        Live.Clear();
        Soon.Clear();
        Going.Clear();
        var nextBoundary = long.MaxValue;
        for (var index = 0; index < contacts.Length; index++)
        {
            var muster = contacts[index];
            if (muster.EndsAtUnix <= nowUnix || IsMine(muster, mine))
            {
                continue;
            }

            Friends.Add(muster);
            nextBoundary = Boundary(nextBoundary, muster, nowUnix);
        }

        for (var index = 0; index < directory.Length; index++)
        {
            var muster = directory[index];
            if (muster.EndsAtUnix <= nowUnix || IsMine(muster, mine) || HostedByContact(contacts, muster.HostId))
            {
                continue;
            }

            nextBoundary = Boundary(nextBoundary, muster, nowUnix);
            if (muster.StartsAtUnix <= nowUnix)
            {
                Live.Add(muster);
            }
            else
            {
                Soon.Add(muster);
            }
        }

        for (var index = 0; index < going.Length; index++)
        {
            var muster = going[index];
            if (muster.EndsAtUnix <= nowUnix || IsMine(muster, mine) || ContainsId(Going, muster.Id))
            {
                continue;
            }

            Going.Add(muster);
            nextBoundary = Boundary(nextBoundary, muster, nowUnix);
        }

        if (mine is not null && mine.EndsAtUnix > nowUnix)
        {
            nextBoundary = Boundary(nextBoundary, mine, nowUnix);
        }

        Soon.Sort(ByStart);
        Going.Sort(ByStart);
        NextRebuildUnix = nextBoundary == long.MaxValue
            ? nowUnix + FallbackRebuildSeconds
            : Math.Min(nextBoundary, nowUnix + FallbackRebuildSeconds);
    }

    private static long Boundary(long current, MusterDto muster, long nowUnix)
    {
        var next = Math.Min(current, muster.EndsAtUnix);
        return muster.StartsAtUnix > nowUnix ? Math.Min(next, muster.StartsAtUnix) : next;
    }

    private static bool IsMine(MusterDto muster, MusterDto? mine) =>
        mine is not null && (muster.Id == mine.Id || muster.HostId == mine.HostId);

    private static bool HostedByContact(MusterDto[] contacts, string hostId)
    {
        for (var index = 0; index < contacts.Length; index++)
        {
            if (contacts[index].HostId == hostId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsId(List<MusterDto> source, string musterId)
    {
        for (var index = 0; index < source.Count; index++)
        {
            if (source[index].Id == musterId)
            {
                return true;
            }
        }

        return false;
    }
}
