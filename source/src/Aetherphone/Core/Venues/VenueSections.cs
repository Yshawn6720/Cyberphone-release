namespace Aetherphone.Core.Venues;

internal readonly record struct VenueSectionsKey(
    int DataVersion,
    int Source,
    IReadOnlySet<string>? DataCenters,
    string ScopeWorld,
    string World,
    int FavoritesStamp,
    int TagsStamp,
    long Minute,
    bool HideAdult = false,
    int RecentsStamp = 0);

internal sealed class VenueSections
{
    public const int MaxFeatured = 6;
    public const int MinFeatured = 3;
    public const int EventWindowDays = 14;
    public const int MaxRail = 12;
    public const int MaxRecents = 12;

    private readonly List<VenueEvent> featured = new();
    private readonly List<VenueEvent> live = new();
    private readonly List<VenueEvent> laterToday = new();
    private readonly List<VenueEvent> nearYou = new();
    private readonly List<VenueEvent> events = new();
    private readonly List<VenueEvent> saved = new();
    private readonly List<VenueEvent> laterRail = new();
    private readonly List<VenueEvent> nearRail = new();
    private readonly List<VenueEvent> recents = new();
    private readonly VenueEvent?[] recentSlots = new VenueEvent?[MaxRecents];
    private readonly int[] categoryCounts = new int[VenueCategories.Count];
    private readonly VenueEvent?[] categoryCovers = new VenueEvent?[VenueCategories.Count];
    private readonly bool[] categoryCoverLive = new bool[VenueCategories.Count];
    private readonly Order liveOrder = new(OrderMode.Live);
    private readonly Order startOrder = new(OrderMode.Start);
    private readonly Order mixedOrder = new(OrderMode.LiveThenStart);
    private readonly Order eventOrder = new(OrderMode.Event);
    private VenueSectionsKey key;
    private bool built;

    public IReadOnlyList<VenueEvent> Featured => featured;
    public IReadOnlyList<VenueEvent> Live => live;
    public IReadOnlyList<VenueEvent> LaterToday => laterToday;
    public IReadOnlyList<VenueEvent> NearYou => nearYou;
    public IReadOnlyList<VenueEvent> Events => events;
    public IReadOnlyList<VenueEvent> Saved => saved;
    public IReadOnlyList<VenueEvent> LaterRail => laterRail;
    public IReadOnlyList<VenueEvent> NearRail => nearRail;
    public IReadOnlyList<VenueEvent> Recents => recents;
    public bool FeaturedIsLive { get; private set; }
    public int Revision { get; private set; }

    public int CategoryCount(int category) => categoryCounts[category];

    public VenueEvent? CategoryCover(int category) => categoryCovers[category];

    public void Invalidate() => built = false;

    public bool Update(in VenueSectionsKey wanted, IReadOnlyList<VenueEvent> source,
        IReadOnlyList<string> favorites, IReadOnlyList<string> recentIds, IReadOnlyList<string> selectedTags,
        DateTime nowUtc)
    {
        if (built && key == wanted)
        {
            return false;
        }

        key = wanted;
        built = true;
        Revision++;
        Clear();
        var today = nowUtc.ToLocalTime().Date;
        var eventHorizon = nowUtc.AddDays(EventWindowDays);
        for (var index = 0; index < source.Count; index++)
        {
            var venue = source[index];
            if (wanted.HideAdult && VenueFilter.IsAdult(venue))
            {
                continue;
            }

            if (VenueFilter.Contains(favorites, venue.Id))
            {
                saved.Add(venue);
            }

            PlaceRecent(venue, recentIds);
            if (!VenueFilter.MatchesScope(venue, wanted.Source, wanted.DataCenters, wanted.ScopeWorld, selectedTags,
                    wanted.HideAdult))
            {
                continue;
            }

            var isLive = venue.IsLive(nowUtc);
            CountCategories(venue, isLive);
            var upcoming = venue.StartUtc is { } start && start > nowUtc;
            if (isLive)
            {
                live.Add(venue);
            }
            else if (upcoming && venue.StartUtc!.Value.ToLocalTime().Date == today)
            {
                laterToday.Add(venue);
            }

            if (wanted.World.Length > 0 && VenueFilter.MatchesWorld(venue, wanted.World))
            {
                nearYou.Add(venue);
            }

            if (venue.EventStartUtc is { } eventStart &&
                (venue.IsEventOn(nowUtc) || (eventStart > nowUtc && eventStart <= eventHorizon)))
            {
                events.Add(venue);
            }
        }

        CompactRecents();

        liveOrder.Now = nowUtc;
        startOrder.Now = nowUtc;
        mixedOrder.Now = nowUtc;
        eventOrder.Now = nowUtc;
        live.Sort(liveOrder);
        laterToday.Sort(startOrder);
        nearYou.Sort(mixedOrder);
        events.Sort(eventOrder);
        saved.Sort(mixedOrder);
        PickFeatured();
        if (FeaturedIsLive)
        {
            FillRail(laterRail, laterToday, null);
        }

        FillRail(nearRail, nearYou, laterRail);
        return true;
    }

    private void FillRail(List<VenueEvent> into, List<VenueEvent> from, List<VenueEvent>? alsoShown)
    {
        for (var index = 0; index < from.Count && into.Count < MaxRail; index++)
        {
            var venue = from[index];
            if (IsIn(featured, venue) || (alsoShown is not null && IsIn(alsoShown, venue)))
            {
                continue;
            }

            into.Add(venue);
        }
    }

    private void Clear()
    {
        featured.Clear();
        live.Clear();
        laterToday.Clear();
        nearYou.Clear();
        events.Clear();
        saved.Clear();
        laterRail.Clear();
        nearRail.Clear();
        recents.Clear();
        Array.Clear(recentSlots);
        Array.Clear(categoryCounts);
        Array.Clear(categoryCovers);
        Array.Clear(categoryCoverLive);
    }

    private void PlaceRecent(VenueEvent venue, IReadOnlyList<string> recentIds)
    {
        var count = Math.Min(recentIds.Count, MaxRecents);
        for (var slot = 0; slot < count; slot++)
        {
            if (string.Equals(recentIds[slot], venue.Id, StringComparison.Ordinal))
            {
                recentSlots[slot] = venue;
                return;
            }
        }
    }

    private void CompactRecents()
    {
        for (var slot = 0; slot < recentSlots.Length; slot++)
        {
            if (recentSlots[slot] is { } venue)
            {
                recents.Add(venue);
            }
        }
    }

    private void CountCategories(VenueEvent venue, bool isLive)
    {
        for (var category = 0; category < VenueCategories.Count; category++)
        {
            if (!VenueCategories.Matches(venue, category))
            {
                continue;
            }

            categoryCounts[category]++;
            var covered = categoryCovers[category] is not null && (categoryCoverLive[category] || !isLive);
            if (venue.BannerUrl is null || covered)
            {
                continue;
            }

            categoryCovers[category] = venue;
            categoryCoverLive[category] = isLive;
        }
    }

    private void PickFeatured()
    {
        FeaturedIsLive = live.Count > 0;
        if (FeaturedIsLive)
        {
            AddFeatured(live, true);
            AddFeatured(live, false);
            return;
        }

        AddFeatured(laterToday, true);
        if (featured.Count < MinFeatured)
        {
            AddFeatured(laterToday, false);
        }
    }

    private void AddFeatured(List<VenueEvent> from, bool needsBanner)
    {
        for (var index = 0; index < from.Count && featured.Count < MaxFeatured; index++)
        {
            var venue = from[index];
            if ((needsBanner && venue.BannerUrl is null) || IsIn(featured, venue))
            {
                continue;
            }

            featured.Add(venue);
        }
    }

    private static bool IsIn(List<VenueEvent> list, VenueEvent venue)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (ReferenceEquals(list[index], venue))
            {
                return true;
            }
        }

        return false;
    }

    private enum OrderMode : byte
    {
        Live,
        Start,
        LiveThenStart,
        Event,
    }

    private sealed class Order(OrderMode mode) : IComparer<VenueEvent>
    {
        public DateTime Now;

        public int Compare(VenueEvent? left, VenueEvent? right)
        {
            if (mode == OrderMode.Event)
            {
                return CompareEvents(left!, right!);
            }

            if (mode != OrderMode.Start)
            {
                var byState = VenueFilter.CompareLiveState(left!, right!, Now);
                if (byState != 0)
                {
                    return byState;
                }
            }

            if (mode == OrderMode.Live)
            {
                var byViewers = right!.LiveViewers.CompareTo(left!.LiveViewers);
                if (byViewers != 0)
                {
                    return byViewers;
                }
            }
            else
            {
                var byStart = StartKey(left!).CompareTo(StartKey(right!));
                if (byStart != 0)
                {
                    return byStart;
                }
            }

            return string.Compare(left!.Title, right!.Title, StringComparison.OrdinalIgnoreCase);
        }

        private DateTime StartKey(VenueEvent venue) =>
            venue.StartUtc is { } start && (mode == OrderMode.Start || start > Now) ? start : DateTime.MaxValue;

        private int CompareEvents(VenueEvent left, VenueEvent right)
        {
            var byOn = right.IsEventOn(Now).CompareTo(left.IsEventOn(Now));
            if (byOn != 0)
            {
                return byOn;
            }

            var byStart = (left.EventStartUtc ?? DateTime.MaxValue).CompareTo(right.EventStartUtc ?? DateTime.MaxValue);
            return byStart != 0 ? byStart : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
        }
    }
}
