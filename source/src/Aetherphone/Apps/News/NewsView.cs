using Aetherphone.Core.News;

namespace Aetherphone.Apps.News;

internal enum NewsScreen : byte
{
    Feed,
    Article,
}

internal readonly record struct NewsView(NewsScreen Screen, NewsCategory Category, string StoryId = "")
{
    public static NewsView Feed(NewsCategory category) => new(NewsScreen.Feed, category);

    public static NewsView Article(NewsCategory category, string storyId) =>
        new(NewsScreen.Article, category, storyId);
}
