using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Social;

internal readonly record struct BadgeProgressRow(
    string Key,
    string BadgeId,
    bool Held,
    float FollowersFraction,
    string FollowersText,
    float LikesFraction,
    string LikesText);

internal sealed class BadgeProgressView
{
    public const string AccountKey = "account";

    public readonly BadgeProgressRow[] Rows;

    private readonly BadgeProgressDto source;
    private readonly string languageCode;

    private BadgeProgressView(BadgeProgressDto source, string languageCode)
    {
        this.source = source;
        this.languageCode = languageCode;
        Rows = new BadgeProgressRow[source.Targets.Length];
        for (var index = 0; index < source.Targets.Length; index++)
        {
            var target = source.Targets[index];
            Rows[index] = new BadgeProgressRow(
                target.Key,
                target.BadgeId,
                target.Held,
                Fraction(target.Followers, target.RequiredFollowers),
                Ratio(target.Followers, target.RequiredFollowers),
                Fraction(target.Likes, target.RequiredLikes),
                Ratio(target.Likes, target.RequiredLikes));
        }
    }

    public static BadgeProgressView From(BadgeProgressDto source)
    {
        return new BadgeProgressView(source, Loc.Current.Code);
    }

    public BadgeProgressView ForCurrentLanguage()
    {
        return languageCode == Loc.Current.Code ? this : new BadgeProgressView(source, Loc.Current.Code);
    }

    private static float Fraction(int value, int required)
    {
        if (required <= 0)
        {
            return 1f;
        }

        return Math.Clamp((float)value / required, 0f, 1f);
    }

    private static string Ratio(int value, int required)
    {
        return value.ToString("N0", Loc.Culture) + " / " + required.ToString("N0", Loc.Culture);
    }
}
