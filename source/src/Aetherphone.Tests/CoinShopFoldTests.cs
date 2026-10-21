using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoinShopFoldTests
{
    [Fact]
    public void InlinedItemsLandOnTheirShelfAndStrayOnesFallToUnfiled()
    {
        var shop = new CoinShopDto(
            new[] { Category("frames", string.Empty), Category("frames.pride", "frames") },
            new[]
            {
                Sku("frame.rainbow", "frames.pride"),
                Sku("case.loose", string.Empty),
                Sku("case.stray", "category-that-was-deleted"),
            },
            true,
            2);

        var shelves = CoinShopFold.Distribute(shop);

        Assert.Single(shelves["frames.pride"]);
        Assert.Equal(2, shelves[CoinCatalogStore.UnfiledId].Length);
        Assert.False(shelves.ContainsKey("frames"));
    }

    [Fact]
    public void OwnedCountsFoldIntoShelvesAndRollUpIntoTheFolder()
    {
        var shop = new CoinShopDto(
            new[] { Category("frames", string.Empty, 2), Category("frames.pride", "frames", 2) },
            new[] { Sku("frame.rainbow", "frames.pride"), Sku("frame.trans", "frames.pride") },
            true);
        var owned = new HashSet<string>(new[] { "frame.trans" }, StringComparer.Ordinal);

        var snapshot = CoinShopFold.Build(shop, CoinShopFold.Distribute(shop), owned);

        var folder = Find(snapshot, "frames");
        var shelf = Find(snapshot, "frames.pride");
        Assert.Equal(1, shelf.OwnedCount);
        Assert.Equal(1, folder.OwnedCount);
        Assert.Equal(2, folder.ItemCount);

        var items = snapshot.Shelves["frames.pride"];
        Assert.True(items[1].Owned);
        Assert.False(items[0].Owned);
    }

    [Fact]
    public void AFolderWithAnUnloadedShelfReportsNoOwnedCount()
    {
        var shop = new CoinShopDto(
            new[] { Category("frames", string.Empty, 3), Category("frames.pride", "frames", 3) },
            Array.Empty<CoinSkuDto>(),
            false);

        var snapshot = CoinShopFold.Build(shop, new Dictionary<string, CoinSkuStyle[]>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

        Assert.Null(Find(snapshot, "frames").OwnedCount);
        Assert.Null(Find(snapshot, "frames.pride").OwnedCount);
        Assert.Equal(3, Find(snapshot, "frames").ItemCount);
    }

    [Fact]
    public void TheUnfiledTileShowsUpOnlyWhenTheShopReportsUnfiledItems()
    {
        var filed = new CoinShopDto(new[] { Category("frames", string.Empty) },
            new[] { Sku("frame.rainbow", "frames") }, true);
        var stray = new CoinShopDto(new[] { Category("frames", string.Empty) },
            new[] { Sku("frame.rainbow", "frames"), Sku("case.loose", string.Empty) }, true, 1);

        var withoutUnfiled = CoinShopFold.Build(filed, CoinShopFold.Distribute(filed), Empty());
        var withUnfiled = CoinShopFold.Build(stray, CoinShopFold.Distribute(stray), Empty());

        Assert.DoesNotContain(withoutUnfiled.Categories, category => category.IsUnfiled);
        var unfiled = Assert.Single(withUnfiled.Categories, category => category.IsUnfiled);
        Assert.Equal(1, unfiled.ItemCount);
        Assert.Equal(0, unfiled.OwnedCount);
        Assert.Equal(int.MaxValue, unfiled.SortOrder);
    }

    private static HashSet<string> Empty()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }

    private static CoinShopCategoryStyle Find(CoinShopSnapshot snapshot, string id)
    {
        for (var index = 0; index < snapshot.Categories.Length; index++)
        {
            if (snapshot.Categories[index].Id == id)
            {
                return snapshot.Categories[index];
            }
        }

        throw new Xunit.Sdk.XunitException("No category " + id);
    }

    private static CoinShopCategoryDto Category(string id, string parentId, int itemCount = 1)
    {
        return new CoinShopCategoryDto(id, parentId, id, 0xF07A, 0, itemCount);
    }

    private static CoinSkuDto Sku(string id, string categoryId)
    {
        return new CoinSkuDto(id, "frame", id, id, 100, 0, false, null, null, categoryId);
    }
}
