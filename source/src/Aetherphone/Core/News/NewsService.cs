using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Game;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.News;

internal enum NewsState : byte
{
    Idle,
    Loading,
    Ready,
    Empty,
    Failed,
}

internal sealed class NewsEntry
{
    public volatile NewsState State = NewsState.Idle;
    public LodestoneNewsItem[] Items = Array.Empty<LodestoneNewsItem>();
    public DateTime FetchedUtc;
    public DateTime LastSuccessUtc;
}

internal sealed class NewsService : IDisposable
{
    private const string ApiRoot = "https://lodestonenews.com/news";
    private const string ChinesePath = "/news/cn/";
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(5);
    private readonly HttpService http;
    private readonly AethernetSession session;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<(NewsCategory Category, string Locale), NewsEntry> entries = new();

    public NewsService(HttpService http, AethernetSession session)
    {
        this.http = http;
        this.session = session;
    }

    public NewsEntry Request(NewsCategory category, string locale, bool forceRefresh)
    {
        var entry = entries.GetOrAdd((category, locale), static _ => new NewsEntry());
        if (entry.State == NewsState.Loading)
        {
            return entry;
        }

        var stale = entry.State == NewsState.Idle || DateTime.UtcNow - entry.FetchedUtc >= FreshFor;
        if (forceRefresh || stale)
        {
            entry.State = NewsState.Loading;
            _ = LoadAsync(category, locale, entry);
        }

        return entry;
    }

    private async Task LoadAsync(NewsCategory category, string locale, NewsEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            var url = FeedUrl(category, locale);
            var items = await http.GetJsonAsync(url, LodestoneNewsJsonContext.Default.NewsItems, null, token)
                .ConfigureAwait(false);
            if (items is null)
            {
                entry.FetchedUtc = DateTime.UtcNow;
                entry.State = NewsState.Failed;
                return;
            }

            var now = DateTime.UtcNow;
            entry.Items = items;
            entry.FetchedUtc = now;
            entry.LastSuccessUtc = now;
            entry.State = items.Length == 0 ? NewsState.Empty : NewsState.Ready;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            entry.FetchedUtc = DateTime.UtcNow;
            entry.State = NewsState.Failed;
            AepLog.Warning(exception, $"News fetch failed for {category}/{locale}");
        }
    }

    // The Lodestone feed carries the international game only. The Chinese one runs its own build on its
    // own maintenance and patch schedule under a different publisher, so those notices would name dates
    // and versions a player there never sees. Aethernet serves that publisher's own news instead.
    private string FeedUrl(NewsCategory category, string locale)
    {
        if (string.Equals(locale, GameData.ChineseLocale, StringComparison.Ordinal))
        {
            return string.Concat(session.BaseUrl.TrimEnd('/'), ChinesePath, NewsCategories.Path(category));
        }

        return string.Concat(ApiRoot, "/", NewsCategories.Path(category), "?locale=", locale);
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
