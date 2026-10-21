using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Coins;

internal sealed class CoinCatalogStore : IDisposable
{
    public const string UnfiledId = "";

    private const long RefreshAfterMilliseconds = 60_000;
    private const long RefreshOnEnterMilliseconds = 5_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;
    private const long ShelfRefreshAfterMilliseconds = 60_000;
    private const int MaxShelfPages = 20;

    private static readonly CoinShopSnapshot Empty = CoinShopSnapshot.Empty;

    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly StoreWork work = new("CoinCatalog");
    private readonly ConcurrentDictionary<string, CoinSkuStyle[]> rawShelves = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> shelfLoadedAt = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> shelfFetching = new(StringComparer.Ordinal);

    private volatile CoinShopSnapshot snapshot = Empty;
    private volatile CoinShopDto? shop;
    private volatile HashSet<string> owned = new(StringComparer.Ordinal);
    private volatile bool loadedOnce;
    private long loadedAtTick;
    private long attemptedAtTick;
    private int fetching;
    private string? lastAccountId;

    public CoinCatalogStore(AethernetSession session, CoinsClient coins)
    {
        this.session = session;
        this.coins = coins;
        session.Changed += OnSessionChanged;
    }

    public CoinShopCategoryStyle[] Categories => snapshot.Categories;

    public bool ItemsComplete => snapshot.ItemsComplete;

    public bool LoadedOnce => loadedOnce;

    public bool Fetching => Volatile.Read(ref fetching) != 0;

    public CoinSkuStyle[] Shelf(string categoryId)
    {
        return snapshot.Shelves.TryGetValue(categoryId, out var items) ? items : Array.Empty<CoinSkuStyle>();
    }

    public bool ShelfLoaded(string categoryId)
    {
        return snapshot.Shelves.ContainsKey(categoryId);
    }

    public CoinShopCategoryStyle? Category(string categoryId)
    {
        var categories = snapshot.Categories;
        for (var index = 0; index < categories.Length; index++)
        {
            if (string.Equals(categories[index].Id, categoryId, StringComparison.Ordinal))
            {
                return categories[index];
            }
        }

        return null;
    }

    public void EnsureFresh()
    {
        Refresh(RefreshAfterMilliseconds);
    }

    public void RefreshOnEnter()
    {
        Refresh(RefreshOnEnterMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref loadedAtTick, 0);
        Interlocked.Exchange(ref attemptedAtTick, 0);
        shelfLoadedAt.Clear();
        Refresh(0);
    }

    public void EnsureShelf(string categoryId)
    {
        if (!session.IsSignedIn || snapshot.ItemsComplete)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (shelfLoadedAt.TryGetValue(categoryId, out var loadedAt)
            && now - loadedAt < ShelfRefreshAfterMilliseconds)
        {
            return;
        }

        if (!shelfFetching.TryAdd(categoryId, 0))
        {
            return;
        }

        work.Run("shelf refresh", async token =>
        {
            var items = new List<CoinSkuStyle>();
            string? cursor = null;
            for (var page = 0; page < MaxShelfPages; page++)
            {
                var response = await coins.ShelfAsync(categoryId, cursor, token).ConfigureAwait(false);
                if (response is null)
                {
                    return;
                }

                for (var index = 0; index < response.Items.Length; index++)
                {
                    items.Add(CoinSkuStyle.From(response.Items[index]));
                }

                cursor = response.NextCursor;
                if (cursor is null)
                {
                    break;
                }
            }

            rawShelves[categoryId] = items.ToArray();
            shelfLoadedAt[categoryId] = Environment.TickCount64;
            Rebuild();
        }, () => shelfFetching.TryRemove(categoryId, out _));
    }

    private void Refresh(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastAttempt = Interlocked.Read(ref attemptedAtTick);
        if (lastAttempt != 0 && now - lastAttempt < RetryAfterAttemptMilliseconds)
        {
            return;
        }

        var lastLoad = Interlocked.Read(ref loadedAtTick);
        if (lastLoad != 0 && now - lastLoad < refreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetching, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref attemptedAtTick, now);
        work.Run("shop refresh", async token =>
        {
            var fetched = await coins.ShopAsync(token).ConfigureAwait(false);
            if (fetched is null)
            {
                return;
            }

            var entitlements = await coins.EntitlementsAsync(token).ConfigureAwait(false);
            if (entitlements is null)
            {
                return;
            }

            shop = fetched;
            owned = new HashSet<string>(entitlements.OwnedSkuIds, StringComparer.Ordinal);
            if (fetched.ItemsComplete)
            {
                Distribute(fetched);
            }

            Rebuild();
            loadedOnce = true;
            Interlocked.Exchange(ref loadedAtTick, Environment.TickCount64);
            Interlocked.Exchange(ref attemptedAtTick, 0);
        }, () => Interlocked.Exchange(ref fetching, 0));
    }

    private void Distribute(CoinShopDto fetched)
    {
        var shelves = CoinShopFold.Distribute(fetched);
        rawShelves.Clear();
        shelfLoadedAt.Clear();
        var stamp = Environment.TickCount64;
        foreach (var shelf in shelves)
        {
            rawShelves[shelf.Key] = shelf.Value;
            shelfLoadedAt[shelf.Key] = stamp;
        }
    }

    private void Rebuild()
    {
        var fetched = shop;
        if (fetched is null)
        {
            return;
        }

        snapshot = CoinShopFold.Build(fetched, rawShelves, owned);
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        shop = null;
        owned = new HashSet<string>(StringComparer.Ordinal);
        rawShelves.Clear();
        shelfLoadedAt.Clear();
        snapshot = Empty;
        loadedOnce = false;
        Interlocked.Exchange(ref loadedAtTick, 0);
        Interlocked.Exchange(ref attemptedAtTick, 0);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}

internal sealed record CoinShopSnapshot(
    CoinShopCategoryStyle[] Categories,
    Dictionary<string, CoinSkuStyle[]> Shelves,
    bool ItemsComplete)
{
    public static readonly CoinShopSnapshot Empty = new(
        Array.Empty<CoinShopCategoryStyle>(),
        new Dictionary<string, CoinSkuStyle[]>(StringComparer.Ordinal),
        true);
}
