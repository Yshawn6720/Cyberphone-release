using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Muster;

internal sealed class MusterLabels
{
    private const int MaxEntries = 256;
    private const long SecondsPerMinute = 60;
    private const string RangeSeparator = " – ";

    private sealed class Entry
    {
        public CachedText Countdown;
        public CachedText Hero;
        public CachedText Count;
        public CachedText Capacity;
        public CachedText Range;
        public CachedText StartClock;
        public CachedText NoticeAgo;
    }

    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public string Countdown(MusterDto muster, long nowUnix)
    {
        if (muster.StartsAtUnix <= nowUnix)
        {
            return Loc.T(L.Common.Live);
        }

        var entry = For(muster.Id);
        var minutes = MinutesUntil(muster.StartsAtUnix, nowUnix);
        return entry.Countdown.IsCurrent(minutes)
            ? entry.Countdown.Value
            : entry.Countdown.Store(minutes, Loc.T(L.Muster.StartsIn, MusterText.Span(minutes * SecondsPerMinute)));
    }

    public string Hero(MusterDto muster, long nowUnix)
    {
        var entry = For(muster.Id);
        var live = muster.StartsAtUnix <= nowUnix;
        var minutes = MinutesUntil(live ? muster.EndsAtUnix : muster.StartsAtUnix, nowUnix);
        var key = live ? -minutes - 1 : minutes;
        if (entry.Hero.IsCurrent(key))
        {
            return entry.Hero.Value;
        }

        var span = MusterText.Span(minutes * SecondsPerMinute);
        return entry.Hero.Store(key, Loc.T(live ? L.Muster.HeroEndsIn : L.Muster.HeroStartsIn, span));
    }

    public string Count(MusterDto muster)
    {
        var entry = For(muster.Id);
        var key = (long)muster.RsvpCount << 20 | (uint)muster.MaxAttendees;
        if (entry.Count.IsCurrent(key))
        {
            return entry.Count.Value;
        }

        var text = muster.MaxAttendees > 0
            ? Loc.T(L.Common.PhotoCounter, muster.RsvpCount, muster.MaxAttendees)
            : Loc.T(L.Muster.GoingCount, muster.RsvpCount);
        return entry.Count.Store(key, text);
    }

    public string Capacity(MusterDto muster)
    {
        var entry = For(muster.Id);
        var key = (long)muster.RsvpCount << 20 | (uint)muster.MaxAttendees;
        if (entry.Capacity.IsCurrent(key))
        {
            return entry.Capacity.Value;
        }

        var text = muster.MaxAttendees > 0
            ? Loc.T(L.Muster.CapacityLine, muster.RsvpCount, muster.MaxAttendees)
            : Loc.T(L.Muster.GoingCount, muster.RsvpCount);
        return entry.Capacity.Store(key, text);
    }

    public string Range(MusterDto muster)
    {
        var entry = For(muster.Id);
        var key = muster.StartsAtUnix * 1_000_003L + (muster.EndsAtUnix - muster.StartsAtUnix);
        if (entry.Range.IsCurrent(key))
        {
            return entry.Range.Value;
        }

        return entry.Range.Store(key, string.Concat(TimeText.Clock(muster.StartsAtUnix), RangeSeparator,
            TimeText.Clock(muster.EndsAtUnix)));
    }

    public string StartClock(MusterDto muster)
    {
        var entry = For(muster.Id);
        return entry.StartClock.IsCurrent(muster.StartsAtUnix)
            ? entry.StartClock.Value
            : entry.StartClock.Store(muster.StartsAtUnix, TimeText.Clock(muster.StartsAtUnix));
    }

    public string NoticeAgo(MusterDto muster, long nowUnix)
    {
        var entry = For(muster.Id);
        var minutes = Math.Max(0, (nowUnix - muster.HostNoticeAtUnix) / SecondsPerMinute);
        var key = minutes * 31 + muster.HostNotice;
        if (entry.NoticeAgo.IsCurrent(key))
        {
            return entry.NoticeAgo.Value;
        }

        return entry.NoticeAgo.Store(key,
            Loc.T(L.Muster.NoticeAgo, MusterText.Span(Math.Max(1, minutes) * SecondsPerMinute)));
    }

    public static string Range(long startsAtUnix, long endsAtUnix, ref CachedText cache)
    {
        var key = startsAtUnix * 1_000_003L + (endsAtUnix - startsAtUnix);
        return cache.IsCurrent(key)
            ? cache.Value
            : cache.Store(key, string.Concat(TimeText.Clock(startsAtUnix), RangeSeparator, TimeText.Clock(endsAtUnix)));
    }

    private static long MinutesUntil(long targetUnix, long nowUnix) =>
        Math.Max(0, (targetUnix - nowUnix + SecondsPerMinute - 1) / SecondsPerMinute);

    private Entry For(string musterId)
    {
        if (entries.TryGetValue(musterId, out var entry))
        {
            return entry;
        }

        if (entries.Count >= MaxEntries)
        {
            entries.Clear();
        }

        entry = new Entry();
        entries[musterId] = entry;
        return entry;
    }
}
