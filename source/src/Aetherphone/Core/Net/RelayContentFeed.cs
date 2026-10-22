using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aetherphone.Core.Net;

internal enum RelayContentStatus : byte
{
    Unconfigured,
    Cached,
    Revalidated,
    Fetched,
    Failed,
}

internal readonly record struct RelayContentResult<T>(RelayContentStatus Status, T? Value, AepFailure Failure)
    where T : class;

internal sealed record RelayContentCacheEntry(string Server, string ETag, long CheckedAtUnixMilliseconds, string Body);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RelayContentCacheEntry))]
internal sealed partial class RelayContentJsonContext : JsonSerializerContext
{
}

internal sealed class RelayContentFeed<T> where T : class
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);
    private const string ContentPathPrefix = "/content/";

    private readonly HttpService http;
    private readonly Configuration configuration;
    private readonly JsonTypeInfo<T> typeInfo;
    private readonly string path;
    private readonly string cachePath;
    private readonly Func<DateTimeOffset> clock;
    private readonly object gate = new();

    private string? checkedUrl;
    private string? checkedInvite;
    private Uri? endpoint;
    private string bearer = string.Empty;
    private string configuredKey = string.Empty;
    private int configurationVersion;
    private Cached? memory;
    private bool diskRead;

    public RelayContentFeed(HttpService http, Configuration configuration, string document, JsonTypeInfo<T> typeInfo,
        string cachePath, Func<DateTimeOffset>? clock = null)
    {
        this.http = http;
        this.configuration = configuration;
        this.typeInfo = typeInfo;
        this.cachePath = cachePath;
        path = ContentPathPrefix + document;
        this.clock = clock ?? (static () => DateTimeOffset.UtcNow);
    }

    public bool IsConfigured
    {
        get
        {
            lock (gate)
            {
                RefreshConfiguration();
                return endpoint is not null;
            }
        }
    }

    public int ConfigurationVersion
    {
        get
        {
            lock (gate)
            {
                RefreshConfiguration();
                return configurationVersion;
            }
        }
    }

    public bool TryGetFresh([NotNullWhen(true)] out T? value)
    {
        lock (gate)
        {
            RefreshConfiguration();
            var cached = endpoint is null ? null : CachedFor(endpoint);
            value = cached is not null && IsFresh(cached.Entry, clock()) ? cached.Value : null;
            return value is not null;
        }
    }

    public async Task<RelayContentResult<T>> LoadAsync(CancellationToken token)
    {
        Uri target;
        string authorization;
        Cached? cached;
        lock (gate)
        {
            RefreshConfiguration();
            if (endpoint is null)
            {
                return new RelayContentResult<T>(RelayContentStatus.Unconfigured, null, AepFailure.None);
            }

            target = endpoint;
            authorization = bearer;
            cached = CachedFor(target);
        }

        var now = clock();
        if (cached is not null && IsFresh(cached.Entry, now))
        {
            return new RelayContentResult<T>(RelayContentStatus.Cached, cached.Value, AepFailure.None);
        }

        var reported = AepFailure.None;
        var response = await http.GetIfChangedAsync(target, authorization, cached?.Entry.ETag, token,
            failure => reported = failure).ConfigureAwait(false);
        if (response is not { } answer)
        {
            var failure = reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline);
            return new RelayContentResult<T>(RelayContentStatus.Failed, cached?.Value, failure);
        }

        var checkedAt = now.ToUnixTimeMilliseconds();
        if (answer.NotModified && cached is not null)
        {
            Remember(new Cached(cached.Entry with { CheckedAtUnixMilliseconds = checkedAt }, cached.Value));
            return new RelayContentResult<T>(RelayContentStatus.Revalidated, cached.Value, AepFailure.None);
        }

        var body = Encoding.UTF8.GetString(answer.Body);
        if (!TryParse(body, target.AbsoluteUri, out var value))
        {
            return new RelayContentResult<T>(RelayContentStatus.Failed, cached?.Value,
                AepFailure.Transport(AepFailureKind.BadResponse));
        }

        Remember(new Cached(new RelayContentCacheEntry(target.AbsoluteUri, answer.ETag, checkedAt, body), value));
        return new RelayContentResult<T>(RelayContentStatus.Fetched, value, AepFailure.None);
    }

    private static bool IsFresh(RelayContentCacheEntry entry, DateTimeOffset now)
    {
        var age = now.ToUnixTimeMilliseconds() - entry.CheckedAtUnixMilliseconds;
        return age >= 0 && age < (long)RefreshInterval.TotalMilliseconds;
    }

    private bool TryParse(string body, string source, [NotNullWhen(true)] out T? value)
    {
        try
        {
            value = JsonSerializer.Deserialize(body, typeInfo);
        }
        catch (JsonException exception)
        {
            AepLog.Warning(exception, $"Relay content from {source} is not readable JSON");
            value = null;
        }

        return value is not null;
    }

    private void RefreshConfiguration()
    {
        var url = configuration.PartyRelayUrl;
        var invite = configuration.PartyRelayInvite;
        if (ReferenceEquals(url, checkedUrl) && ReferenceEquals(invite, checkedInvite))
        {
            return;
        }

        checkedUrl = url;
        checkedInvite = invite;
        Uri? created = null;
        var usable = !string.IsNullOrWhiteSpace(invite) && RelayAddress.TryCreate(url, path, false, out created);
        endpoint = usable ? created : null;
        bearer = usable ? invite.Trim() : string.Empty;
        var key = endpoint is null ? string.Empty : string.Concat(endpoint.AbsoluteUri, "\n", bearer);
        if (!string.Equals(key, configuredKey, StringComparison.Ordinal))
        {
            configuredKey = key;
            configurationVersion++;
        }
    }

    private Cached? CachedFor(Uri target)
    {
        if (!diskRead)
        {
            diskRead = true;
            memory = ReadDisk();
        }

        return memory is not null && string.Equals(memory.Entry.Server, target.AbsoluteUri, StringComparison.Ordinal)
            ? memory
            : null;
    }

    private Cached? ReadDisk()
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        RelayContentCacheEntry? entry;
        try
        {
            entry = JsonSerializer.Deserialize(File.ReadAllBytes(cachePath),
                RelayContentJsonContext.Default.RelayContentCacheEntry);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            AepLog.Warning(exception, $"Relay content cache {cachePath} is unreadable; it will be fetched again");
            entry = null;
        }

        if (entry is null || string.IsNullOrEmpty(entry.Server) || string.IsNullOrEmpty(entry.Body))
        {
            return null;
        }

        return TryParse(entry.Body, cachePath, out var value) ? new Cached(entry, value) : null;
    }

    private void Remember(Cached cached)
    {
        lock (gate)
        {
            memory = cached;
            diskRead = true;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath) ?? ".");
            File.WriteAllBytes(cachePath,
                JsonSerializer.SerializeToUtf8Bytes(cached.Entry, RelayContentJsonContext.Default.RelayContentCacheEntry));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Warning(exception, $"Relay content cache {cachePath} could not be written");
        }
    }

    private sealed record Cached(RelayContentCacheEntry Entry, T Value);
}
