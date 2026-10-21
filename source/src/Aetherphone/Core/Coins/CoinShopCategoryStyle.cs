using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Coins;

internal sealed record CoinShopCategoryStyle(
    string Id,
    string ParentId,
    string BaseName,
    long Icon,
    string ImageUrl,
    int SortOrder,
    int ItemCount,
    int? OwnedCount,
    long? SoonestLeavingUnix,
    CoinTranslationDto[]? Translations)
{
    public bool IsUnfiled => Id.Length == 0;

    public string Name
    {
        get
        {
            if (Translations is null)
            {
                return BaseName;
            }

            var code = Loc.Current.Code;
            for (var index = 0; index < Translations.Length; index++)
            {
                var translation = Translations[index];
                if (translation.Language == code && !string.IsNullOrEmpty(translation.Name))
                {
                    return translation.Name;
                }
            }

            return BaseName;
        }
    }

    public static CoinShopCategoryStyle From(CoinShopCategoryDto category, int? ownedCount)
    {
        return new CoinShopCategoryStyle(
            category.Id,
            category.ParentId,
            category.Name,
            category.Icon,
            category.ImageUrl,
            category.SortOrder,
            category.ItemCount,
            ownedCount,
            category.SoonestLeavingUnix,
            category.Translations);
    }

    public static CoinShopCategoryStyle Unfiled(int itemCount, int? ownedCount, long? soonestLeavingUnix)
    {
        return new CoinShopCategoryStyle(string.Empty, string.Empty, string.Empty, 0, string.Empty, int.MaxValue,
            itemCount, ownedCount, soonestLeavingUnix, null);
    }
}
