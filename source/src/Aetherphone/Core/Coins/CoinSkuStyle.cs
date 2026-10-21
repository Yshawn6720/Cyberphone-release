using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Coins;

internal sealed record CoinSkuStyle(
    string Id,
    string Kind,
    string Payload,
    string BaseName,
    long Price,
    bool Owned,
    long? AvailableUntilUnix,
    CoinTranslationDto[]? Translations)
{
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

    public static CoinSkuStyle From(CoinSkuDto sku)
    {
        return new CoinSkuStyle(
            sku.Id,
            sku.Kind,
            sku.Payload,
            sku.Name,
            sku.Price,
            sku.Owned,
            sku.AvailableUntilUnix,
            sku.Translations);
    }
}
