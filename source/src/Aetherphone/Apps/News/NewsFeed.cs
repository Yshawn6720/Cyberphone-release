using System.Globalization;
using Aetherphone.Core.Localization;
using Aetherphone.Core.News;

namespace Aetherphone.Apps.News;

internal sealed class NewsFeed
{
    private readonly Dictionary<string, NewsStory> cache;
    private LodestoneNewsItem[]? source;
    private CultureInfo? culture;
    private int formatVersion = -1;
    private DateTime day;

    public NewsFeed(NewsCategory category, Dictionary<string, NewsStory> cache)
    {
        Category = category;
        this.cache = cache;
    }

    public NewsCategory Category { get; }

    public NewsStory[] Stories { get; private set; } = Array.Empty<NewsStory>();

    public MaintenanceBoard Board { get; } = new();

    public NewsEntry? Entry { get; private set; }

    public NewsState State => Entry?.State ?? NewsState.Idle;

    public bool HasStories => Stories.Length > 0;

    public void Sync(NewsEntry entry)
    {
        Entry = entry;
        var items = entry.Items;
        var today = DateTime.Now.Date;
        if (ReferenceEquals(items, source) && ReferenceEquals(culture, Loc.Culture) &&
            formatVersion == TimeText.FormatVersion && day == today)
        {
            return;
        }

        var rebuilt = Stories.Length == items.Length ? Stories : new NewsStory[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var key = item.Id.Length > 0 ? item.Id : item.Url;
            if (!cache.TryGetValue(key, out var story))
            {
                story = new NewsStory();
                cache[key] = story;
            }

            story.Rebuild(item);
            rebuilt[index] = story;
        }

        Stories = rebuilt;
        Board.Sync(items);
        source = items;
        culture = Loc.Culture;
        formatVersion = TimeText.FormatVersion;
        day = today;
    }

    public int IndexOf(string storyId)
    {
        for (var index = 0; index < Stories.Length; index++)
        {
            if (string.Equals(Stories[index].Id, storyId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
