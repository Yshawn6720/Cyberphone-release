using System.Globalization;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Coin;

internal sealed class CoinTextCache
{
    private const int Limit = 256;

    private readonly Dictionary<long, string> leaving = new();
    private readonly Dictionary<long, string> counters = new();
    private readonly Dictionary<long, string> buyLabels = new();
    private readonly Dictionary<long, string> remaining = new();
    private CultureInfo? culture;
    private int timeFormat = -1;
    private DateTime day;

    public string Leaving(long leavingUnix)
    {
        Validate();
        if (leaving.TryGetValue(leavingUnix, out var cached))
        {
            return cached;
        }

        return Remember(leaving, leavingUnix, Loc.T(L.Coin.LeavingSoon, TimeText.FutureDayLabel(leavingUnix)));
    }

    public string Counter(int? owned, int itemCount)
    {
        Validate();
        var key = owned is { } value ? ((long)(value + 1) << 32) | (uint)itemCount : itemCount;
        if (counters.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var text = owned is { } ownedCount
            ? Loc.T(L.Coin.SectionOwned, ownedCount, itemCount)
            : Loc.Plural(L.Coin.ShopItemCount, itemCount);
        return Remember(counters, key, text);
    }

    public string BuyFor(long price)
    {
        Validate();
        if (buyLabels.TryGetValue(price, out var cached))
        {
            return cached;
        }

        return Remember(buyLabels, price, Loc.T(L.Coin.BuyFor, Loc.Plural(L.Coin.Price, (int)price)));
    }

    public string Remaining(long amount)
    {
        Validate();
        if (remaining.TryGetValue(amount, out var cached))
        {
            return cached;
        }

        return Remember(remaining, amount, Loc.T(L.Coin.GoalRemaining, NumberText.Group(amount)));
    }

    private static string Remember(Dictionary<long, string> cache, long key, string text)
    {
        if (cache.Count >= Limit)
        {
            cache.Clear();
        }

        cache[key] = text;
        return text;
    }

    private void Validate()
    {
        var today = DateTime.Now.Date;
        if (ReferenceEquals(culture, Loc.Culture) && timeFormat == TimeText.FormatVersion && today == day)
        {
            return;
        }

        culture = Loc.Culture;
        timeFormat = TimeText.FormatVersion;
        day = today;
        leaving.Clear();
        counters.Clear();
        buyLabels.Clear();
        remaining.Clear();
    }
}
