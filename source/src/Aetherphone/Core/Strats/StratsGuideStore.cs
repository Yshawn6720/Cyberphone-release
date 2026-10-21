using System.Collections.Concurrent;
using System.Text.Json;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Strats;

internal sealed class GuideEntry
{
    public volatile StratsState State = StratsState.Idle;
    public volatile FightDoc? Doc;
}

internal sealed class StratsGuideStore : IDisposable
{
    private static readonly TimeSpan Immutable = TimeSpan.FromDays(3650);
    private readonly HttpService http;
    private readonly DiskCache disk;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<string, GuideEntry> entries = new(StringComparer.Ordinal);

    public StratsGuideStore(HttpService http, DiskCache disk)
    {
        this.http = http;
        this.disk = disk;
    }

    public GuideEntry Request(ManifestFight fight, bool forceRefresh)
    {
        var entry = entries.GetOrAdd(fight.GuideKey, static _ => new GuideEntry());
        if (entry.State == StratsState.Loading)
        {
            return entry;
        }

        if (entry.State == StratsState.Ready && !forceRefresh)
        {
            return entry;
        }

        entry.State = StratsState.Loading;
        _ = LoadAsync(fight.GuideKey, entry, forceRefresh);
        return entry;
    }

    private async Task LoadAsync(string guideKey, GuideEntry entry, bool forceRefresh)
    {
        try
        {
            var token = cancellation.Token;
            var diskKey = string.Concat("strats:guide:", guideKey);
            var bytes = forceRefresh ? null : disk.Get(diskKey, Immutable);
            if (bytes is null)
            {
                bytes = await http.GetBytesAsync(new Uri(StratsContent.Url(guideKey)), token).ConfigureAwait(false);
                if (bytes is null)
                {
                    entry.State = StratsState.Failed;
                    return;
                }
            }

            var doc = JsonSerializer.Deserialize(bytes, StratsJsonContext.Default.FightDoc);
            if (doc is null || doc.SchemaVersion != StratsContent.SchemaVersion || doc.Strats.Length == 0)
            {
                entry.State = StratsState.Failed;
                return;
            }

            disk.Set(diskKey, bytes);
            entry.Doc = doc;
            entry.State = StratsState.Ready;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            entry.State = StratsState.Failed;
            AepLog.Warning(exception, $"Strats guide fetch failed for {guideKey}");
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
