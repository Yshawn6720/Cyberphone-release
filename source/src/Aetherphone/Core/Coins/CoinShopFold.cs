using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Coins;

internal static class CoinShopFold
{
    public static Dictionary<string, CoinSkuStyle[]> Distribute(CoinShopDto shop)
    {
        var buckets = new Dictionary<string, List<CoinSkuStyle>>(StringComparer.Ordinal);
        for (var index = 0; index < shop.Items.Length; index++)
        {
            var item = shop.Items[index];
            var shelfId = ShelfIdFor(shop.Categories, item.CategoryId);
            if (!buckets.TryGetValue(shelfId, out var bucket))
            {
                bucket = new List<CoinSkuStyle>();
                buckets[shelfId] = bucket;
            }

            bucket.Add(CoinSkuStyle.From(item));
        }

        var shelves = new Dictionary<string, CoinSkuStyle[]>(buckets.Count, StringComparer.Ordinal);
        foreach (var bucket in buckets)
        {
            shelves[bucket.Key] = bucket.Value.ToArray();
        }

        return shelves;
    }

    public static CoinShopSnapshot Build(CoinShopDto shop, IReadOnlyDictionary<string, CoinSkuStyle[]> rawShelves,
        HashSet<string> owned)
    {
        var shelves = new Dictionary<string, CoinSkuStyle[]>(rawShelves.Count, StringComparer.Ordinal);
        var ownedCounts = new Dictionary<string, int>(rawShelves.Count, StringComparer.Ordinal);
        foreach (var raw in rawShelves)
        {
            var source = raw.Value;
            var marked = new CoinSkuStyle[source.Length];
            var ownedHere = 0;
            for (var index = 0; index < source.Length; index++)
            {
                var isOwned = owned.Contains(source[index].Id);
                marked[index] = source[index] with { Owned = isOwned };
                if (isOwned)
                {
                    ownedHere++;
                }
            }

            shelves[raw.Key] = marked;
            ownedCounts[raw.Key] = ownedHere;
        }

        var categories = new List<CoinShopCategoryStyle>(shop.Categories.Length + 1);
        for (var index = 0; index < shop.Categories.Length; index++)
        {
            var category = shop.Categories[index];
            categories.Add(CoinShopCategoryStyle.From(category, OwnedFor(shop, ownedCounts, category)));
        }

        if (shop.UnfiledCount > 0)
        {
            var known = ownedCounts.TryGetValue(CoinCatalogStore.UnfiledId, out var unfiledOwned);
            categories.Add(CoinShopCategoryStyle.Unfiled(shop.UnfiledCount, known ? unfiledOwned : null,
                shop.UnfiledSoonestLeavingUnix));
        }

        return new CoinShopSnapshot(categories.ToArray(), shelves, shop.ItemsComplete);
    }

    private static string ShelfIdFor(CoinShopCategoryDto[] categories, string categoryId)
    {
        if (categoryId.Length == 0)
        {
            return CoinCatalogStore.UnfiledId;
        }

        for (var index = 0; index < categories.Length; index++)
        {
            if (string.Equals(categories[index].Id, categoryId, StringComparison.Ordinal))
            {
                return categoryId;
            }
        }

        return CoinCatalogStore.UnfiledId;
    }

    private static int? OwnedFor(CoinShopDto shop, Dictionary<string, int> ownedCounts,
        CoinShopCategoryDto category)
    {
        var children = 0;
        var total = 0;
        for (var index = 0; index < shop.Categories.Length; index++)
        {
            var candidate = shop.Categories[index];
            if (!string.Equals(candidate.ParentId, category.Id, StringComparison.Ordinal))
            {
                continue;
            }

            children++;
            if (!ownedCounts.TryGetValue(candidate.Id, out var childOwned))
            {
                return null;
            }

            total += childOwned;
        }

        if (children > 0)
        {
            return total;
        }

        return ownedCounts.TryGetValue(category.Id, out var ownedHere) ? ownedHere : null;
    }
}
