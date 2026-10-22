using System.Text.Json.Serialization;

namespace Aetherphone.Core.News;

internal sealed class TaiwanNewsDocument
{
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; set; }
    [JsonPropertyName("topics")] public LodestoneNewsItem[]? Topics { get; set; }
    [JsonPropertyName("notices")] public LodestoneNewsItem[]? Notices { get; set; }
    [JsonPropertyName("maintenance")] public LodestoneNewsItem[]? Maintenance { get; set; }
    [JsonPropertyName("updates")] public LodestoneNewsItem[]? Updates { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TaiwanNewsDocument))]
internal sealed partial class TaiwanNewsJsonContext : JsonSerializerContext
{
}

internal sealed class TaiwanNewsBoard
{
    public static readonly TaiwanNewsBoard Empty = new(null, new LodestoneNewsItem[NewsCategories.All.Length][]);

    private readonly LodestoneNewsItem[][] lists;

    private TaiwanNewsBoard(TaiwanNewsDocument? source, LodestoneNewsItem[][] lists)
    {
        Source = source;
        this.lists = lists;
    }

    public TaiwanNewsDocument? Source { get; }

    public static TaiwanNewsBoard From(TaiwanNewsDocument document)
    {
        var lists = new LodestoneNewsItem[NewsCategories.All.Length][];
        lists[(int)NewsCategory.Topics] = Readable(document.Topics);
        lists[(int)NewsCategory.Notices] = Readable(document.Notices);
        lists[(int)NewsCategory.Maintenance] = Readable(document.Maintenance);
        lists[(int)NewsCategory.Updates] = Readable(document.Updates);
        return new TaiwanNewsBoard(document, lists);
    }

    public LodestoneNewsItem[] ItemsFor(NewsCategory category)
    {
        var index = (int)category;
        return index < lists.Length ? lists[index] ?? Array.Empty<LodestoneNewsItem>() : Array.Empty<LodestoneNewsItem>();
    }

    private static LodestoneNewsItem[] Readable(LodestoneNewsItem[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return Array.Empty<LodestoneNewsItem>();
        }

        var kept = new List<LodestoneNewsItem>(items.Length);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item is null || string.IsNullOrEmpty(item.Id) || string.IsNullOrWhiteSpace(item.Title) ||
                item.Time == default || !IsWebLink(item.Url))
            {
                continue;
            }

            item.Image = null;
            kept.Add(item);
        }

        var readable = kept.ToArray();
        Array.Sort(readable, NewestFirst);
        return readable;
    }

    private static bool IsWebLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static int NewestFirst(LodestoneNewsItem left, LodestoneNewsItem right)
    {
        var byTime = right.Time.CompareTo(left.Time);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }
}
