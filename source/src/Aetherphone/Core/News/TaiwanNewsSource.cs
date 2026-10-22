using Aetherphone.Core.Net;

namespace Aetherphone.Core.News;

internal sealed class TaiwanNewsSource : IDisposable
{
    public const string Document = "news-tw";
    public const string CacheFileName = "relay_news_tw.json";
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    private readonly RelayContentFeed<TaiwanNewsDocument> feed;
    private readonly Func<DateTimeOffset> clock;
    private readonly CancellationTokenSource cancellation = new();
    private readonly NewsEntry[] entries = new NewsEntry[NewsCategories.All.Length];
    private readonly object gate = new();
    private TaiwanNewsBoard board = TaiwanNewsBoard.Empty;
    private int configurationVersion = int.MinValue;
    private bool loading;
    private bool failed;
    private bool unsupported;
    private DateTimeOffset retryAt;
    private DateTime updatedUtc;

    public TaiwanNewsSource(HttpService http, Configuration configuration, string cachePath,
        Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (static () => DateTimeOffset.UtcNow);
        feed = new RelayContentFeed<TaiwanNewsDocument>(http, configuration, Document,
            TaiwanNewsJsonContext.Default.TaiwanNewsDocument, cachePath, this.clock);
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = new NewsEntry();
        }
    }

    public Task Settled { get; private set; } = Task.CompletedTask;

    public NewsEntry Request(NewsCategory category, bool forceRefresh, bool mayConnect)
    {
        var index = (int)category;
        var entry = entries[index < entries.Length ? index : 0];
        lock (gate)
        {
            if (!feed.IsConfigured)
            {
                Forget(int.MinValue);
                entry.Items = Array.Empty<LodestoneNewsItem>();
                entry.State = NewsState.Unconfigured;
                return entry;
            }

            var version = feed.ConfigurationVersion;
            if (version != configurationVersion)
            {
                Forget(version);
            }

            if (feed.TryGetFresh(out var document))
            {
                Adopt(document);
                failed = false;
                unsupported = false;
            }
            else if (mayConnect && !loading && (forceRefresh || clock() >= retryAt))
            {
                loading = true;
                Settled = LoadAsync(version);
            }

            Fill(entry, category);
        }

        return entry;
    }

    private async Task LoadAsync(int version)
    {
        RelayContentResult<TaiwanNewsDocument> result;
        try
        {
            result = await feed.LoadAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            lock (gate)
            {
                loading = false;
            }

            return;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Traditional Chinese news could not be loaded from the relay");
            lock (gate)
            {
                loading = false;
                failed = version == configurationVersion;
                unsupported = false;
                retryAt = clock() + RetryAfter;
            }

            return;
        }

        lock (gate)
        {
            loading = false;
            if (version != configurationVersion)
            {
                return;
            }

            if (result.Value is { } document)
            {
                Adopt(document);
            }

            unsupported = result.Status == RelayContentStatus.Unsupported;
            failed = unsupported || result.Status == RelayContentStatus.Failed;
            if (failed)
            {
                retryAt = clock() + RetryAfter;
            }
        }
    }

    private void Forget(int version)
    {
        if (version == configurationVersion)
        {
            return;
        }

        configurationVersion = version;
        board = TaiwanNewsBoard.Empty;
        failed = false;
        unsupported = false;
        retryAt = default;
        updatedUtc = default;
    }

    private void Adopt(TaiwanNewsDocument document)
    {
        if (ReferenceEquals(board.Source, document))
        {
            return;
        }

        board = TaiwanNewsBoard.From(document);
        updatedUtc = document.UpdatedAt == default ? clock().UtcDateTime : document.UpdatedAt.UtcDateTime;
    }

    private void Fill(NewsEntry entry, NewsCategory category)
    {
        var items = board.ItemsFor(category);
        entry.Items = items;
        entry.FetchedUtc = updatedUtc;
        entry.LastSuccessUtc = updatedUtc;
        entry.State = loading
            ? NewsState.Loading
            : unsupported
                ? NewsState.Unsupported
                : failed
                    ? NewsState.Failed
                    : items.Length > 0
                        ? NewsState.Ready
                        : NewsState.Empty;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
