using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Social;

internal sealed record FrameStyle(
    string Id,
    string BaseName,
    BadgeTranslationDto[]? Translations,
    string AssetUrl,
    float Scale)
{
    public const int MinScalePercent = 100;

    public const int MaxScalePercent = 200;

    public const int DefaultScalePercent = 138;

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
                if (translation.Lang == code && !string.IsNullOrEmpty(translation.Name))
                {
                    return translation.Name;
                }
            }

            return BaseName;
        }
    }

    public static FrameStyle From(FrameDescriptorDto descriptor)
    {
        return new FrameStyle(
            descriptor.Id,
            descriptor.Name,
            descriptor.Translations,
            descriptor.AssetUrl,
            ScaleOf(descriptor.ScalePercent));
    }

    public static float ScaleOf(int scalePercent)
    {
        return Math.Clamp(scalePercent, MinScalePercent, MaxScalePercent) / 100f;
    }
}
