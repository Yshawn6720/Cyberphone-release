using System.Text.Json.Serialization;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Radio;

internal sealed class RelayRadioDocument
{
    [JsonPropertyName("stations")] public RelayRadioEntry[]? Stations { get; set; }
}

internal sealed class RelayRadioEntry
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("streamUrl")] public string? StreamUrl { get; set; }
    [JsonPropertyName("genre")] public string? Genre { get; set; }
    [JsonPropertyName("homepage")] public string? Homepage { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RelayRadioDocument))]
internal sealed partial class RelayRadioJsonContext : JsonSerializerContext
{
}

internal sealed class RelayRadioList
{
    public static readonly RelayRadioList Empty = new(null, Array.Empty<RadioStation>(), Array.Empty<string>());

    public RelayRadioList(RelayRadioDocument? source, RadioStation[] stations, string[] homepages)
    {
        Source = source;
        Stations = stations;
        Homepages = homepages;
    }

    public RelayRadioDocument? Source { get; }

    public RadioStation[] Stations { get; }

    public string[] Homepages { get; }
}

internal sealed class RelayRadioStations : IDisposable
{
    public const string Document = "radio";
    public const string CacheFileName = "relay_radio.json";
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    private readonly RelayContentFeed<RelayRadioDocument> feed;
    private readonly Func<DateTimeOffset> clock;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object gate = new();
    private int configurationVersion = int.MinValue;
    private DateTimeOffset retryAt;
    private volatile RelayRadioList list = RelayRadioList.Empty;
    private volatile bool loading;
    private volatile bool failed;
    private volatile bool unsupported;

    public RelayRadioStations(HttpService http, Configuration configuration, string cachePath,
        Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (static () => DateTimeOffset.UtcNow);
        feed = new RelayContentFeed<RelayRadioDocument>(http, configuration, Document,
            RelayRadioJsonContext.Default.RelayRadioDocument, cachePath, this.clock);
    }

    public RelayRadioList List => list;

    public bool Loading => loading;

    public bool Failed => failed;

    public bool Unsupported => unsupported;

    public Task Settled { get; private set; } = Task.CompletedTask;

    public bool EnsureFresh() => Refresh(false);

    public void Retry() => Refresh(true);

    private bool Refresh(bool force)
    {
        lock (gate)
        {
            if (!feed.IsConfigured)
            {
                Forget(int.MinValue);
                return false;
            }

            var version = feed.ConfigurationVersion;
            if (version != configurationVersion)
            {
                Forget(version);
            }

            if (loading)
            {
                return true;
            }

            if (feed.TryGetFresh(out var document))
            {
                Adopt(document);
                failed = false;
                unsupported = false;
                return true;
            }

            if (!force && failed && clock() < retryAt)
            {
                return true;
            }

            loading = true;
            Settled = LoadAsync(version);
            return true;
        }
    }

    public static RelayRadioList Readable(RelayRadioDocument document)
    {
        var entries = document.Stations;
        if (entries is null || entries.Length == 0)
        {
            return new RelayRadioList(document, Array.Empty<RadioStation>(), Array.Empty<string>());
        }

        var stations = new List<RadioStation>(entries.Length);
        var homepages = new List<string>(entries.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var name = entry?.Name?.Trim();
            var streamUrl = entry?.StreamUrl?.Trim();
            if (entry is null || string.IsNullOrEmpty(name) || !IsWebLink(streamUrl) || !seen.Add(streamUrl!))
            {
                continue;
            }

            var genre = entry.Genre?.Trim() ?? string.Empty;
            var homepage = entry.Homepage?.Trim();
            stations.Add(new RadioStation(name, streamUrl!, string.Empty, 0, string.Empty, string.Empty, string.Empty,
                string.Empty, genre, string.Empty, string.Empty, 0, 0));
            homepages.Add(IsWebLink(homepage) ? homepage! : string.Empty);
        }

        return new RelayRadioList(document, stations.ToArray(), homepages.ToArray());
    }

    private async Task LoadAsync(int version)
    {
        RelayContentResult<RelayRadioDocument> result;
        try
        {
            result = await feed.LoadAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            loading = false;
            return;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Relay radio stations could not be loaded");
            result = new RelayContentResult<RelayRadioDocument>(RelayContentStatus.Failed, null,
                AepFailure.Transport(AepFailureKind.Offline));
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
        list = RelayRadioList.Empty;
        failed = false;
        unsupported = false;
        retryAt = default;
    }

    private void Adopt(RelayRadioDocument document)
    {
        if (!ReferenceEquals(list.Source, document))
        {
            list = Readable(document);
        }
    }

    private static bool IsWebLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
