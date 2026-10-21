using Aetherphone.Core.Localization;

namespace Aetherphone.Core.News;

internal enum NewsCategory : byte
{
    Topics,
    Notices,
    Maintenance,
    Updates,
    Status,
}

internal static class NewsCategories
{
    public static readonly NewsCategory[] All =
    {
        NewsCategory.Topics,
        NewsCategory.Notices,
        NewsCategory.Maintenance,
        NewsCategory.Updates,
        NewsCategory.Status,
    };

    public static string Path(NewsCategory category)
    {
        return category switch
        {
            NewsCategory.Notices => "notices",
            NewsCategory.Maintenance => "maintenance",
            NewsCategory.Updates => "updates",
            NewsCategory.Status => "status",
            _ => "topics",
        };
    }

    public static LocString Label(NewsCategory category)
    {
        return category switch
        {
            NewsCategory.Notices => L.News.Notices,
            NewsCategory.Maintenance => L.News.Maintenance,
            NewsCategory.Updates => L.News.Updates,
            NewsCategory.Status => L.News.Status,
            _ => L.News.Topics,
        };
    }

    public static bool TryParse(string path, out NewsCategory category)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(Path(All[index]), path, StringComparison.Ordinal))
            {
                category = All[index];
                return true;
            }
        }

        category = NewsCategory.Topics;
        return false;
    }

    public static bool AvailableFor(NewsCategory category, string locale) =>
        category != NewsCategory.Status || !string.Equals(locale, Game.GameData.ChineseLocale, StringComparison.Ordinal);
}
