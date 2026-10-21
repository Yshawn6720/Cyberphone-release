using System.Text.Json;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Strats;

internal enum StratsState : byte
{
    Idle,
    Loading,
    Ready,
    Failed,
}

internal sealed class StratsManifestStore : IDisposable
{
    private const string DiskKey = "strats:manifest";
    private static readonly TimeSpan FreshFor = TimeSpan.FromHours(6);
    private static readonly TimeSpan StaleFallback = TimeSpan.FromDays(3650);
    private readonly HttpService http;
    private readonly DiskCache disk;
    private readonly CancellationTokenSource cancellation = new();
    private int refreshing;
    private volatile StratsManifest? manifest;
    private volatile StratsState state = StratsState.Idle;
    private volatile int version;
    private DateTime lastRefreshUtc;

    public StratsManifestStore(HttpService http, DiskCache disk)
    {
        this.http = http;
        this.disk = disk;
    }

    public StratsManifest? Manifest => manifest;
    public StratsState State => state;
    public int Version => version;

    public void EnsureFresh(bool force)
    {
        if (Volatile.Read(ref refreshing) == 1)
        {
            return;
        }

        var stale = state == StratsState.Idle || DateTime.UtcNow - lastRefreshUtc >= FreshFor;
        if (!force && !stale)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0)
        {
            return;
        }

        if (state != StratsState.Ready)
        {
            state = StratsState.Loading;
        }

        _ = RefreshAsync(force);
    }

    public bool TryFind(string fightKey, out ManifestFight fight)
    {
        var current = manifest;
        if (current is not null)
        {
            for (var groupIndex = 0; groupIndex < current.Groups.Length; groupIndex++)
            {
                var fights = current.Groups[groupIndex].Fights;
                for (var fightIndex = 0; fightIndex < fights.Length; fightIndex++)
                {
                    if (string.Equals(fights[fightIndex].Key, fightKey, StringComparison.Ordinal))
                    {
                        fight = fights[fightIndex];
                        return true;
                    }
                }
            }
        }

        fight = null!;
        return false;
    }

    private async Task RefreshAsync(bool force)
    {
        try
        {
            var token = cancellation.Token;
            if (!force && Publish(disk.Get(DiskKey, FreshFor)))
            {
                return;
            }

            var bytes = await http.GetBytesAsync(new Uri(StratsContent.ManifestUrl(DateTime.UtcNow)), token)
                .ConfigureAwait(false);
            if (Publish(bytes))
            {
                disk.Set(DiskKey, bytes!);
                return;
            }

            if (!Publish(disk.Get(DiskKey, StaleFallback)))
            {
                state = StratsState.Failed;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            state = manifest is null ? StratsState.Failed : StratsState.Ready;
            AepLog.Warning(exception, "Strats manifest fetch failed");
        }
        finally
        {
            lastRefreshUtc = DateTime.UtcNow;
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private bool Publish(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return false;
        }

        StratsManifest? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(bytes, StratsJsonContext.Default.StratsManifest);
        }
        catch (JsonException exception)
        {
            AepLog.Warning(exception, "Strats manifest could not be parsed");
            return false;
        }

        if (parsed is null || parsed.SchemaVersion != StratsContent.SchemaVersion)
        {
            return false;
        }

        manifest = parsed;
        version++;
        state = StratsState.Ready;
        return true;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
