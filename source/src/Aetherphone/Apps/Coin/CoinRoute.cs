namespace Aetherphone.Apps.Coin;

internal enum CoinScreen : byte
{
    Root,
    ShopFolder,
    ShopShelf,
    Product,
    Entry,
}

internal readonly record struct CoinRoute(CoinScreen Screen, string CategoryId, string ItemId)
{
    public static readonly CoinRoute Root = new(CoinScreen.Root, string.Empty, string.Empty);

    public static CoinRoute Folder(string categoryId) => new(CoinScreen.ShopFolder, categoryId, string.Empty);

    public static CoinRoute Shelf(string categoryId) => new(CoinScreen.ShopShelf, categoryId, string.Empty);

    public static CoinRoute Product(string categoryId, string skuId) => new(CoinScreen.Product, categoryId, skuId);

    public static CoinRoute Entry(string entryId) => new(CoinScreen.Entry, string.Empty, entryId);
}
