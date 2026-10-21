using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;

namespace Aetherphone.Core.Social;

internal sealed class FrameCatalogStore : IDisposable
{
    private const long RefreshAfterMilliseconds = 600_000;
    private const long RefreshAfterUnknownIdMilliseconds = 30_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly AethernetSession session;
    private readonly AccountClient account;
    private readonly StoreWork work = new("FrameCatalog");

    private volatile Dictionary<string, FrameStyle> stylesById = new(StringComparer.Ordinal);
    private volatile string[] patreonFrameIds = Array.Empty<string>();
    private long loadedAtTick;
    private long attemptedAtTick;
    private int fetching;

    public FrameCatalogStore(AethernetSession session, AccountClient account)
    {
        this.session = session;
        this.account = account;
    }

    public FrameStyle? Find(string? frameId)
    {
        if (string.IsNullOrEmpty(frameId))
        {
            return null;
        }

        if (stylesById.TryGetValue(frameId, out var style))
        {
            EnsureFresh();
            return style;
        }

        Refresh(RefreshAfterUnknownIdMilliseconds);
        return null;
    }

    public FrameStyle? FindPatreonFrame(int tierIndex)
    {
        var frameIds = patreonFrameIds;
        if (tierIndex < 0 || tierIndex >= frameIds.Length)
        {
            EnsureFresh();
            return null;
        }

        return Find(frameIds[tierIndex]);
    }

    public void EnsureFresh()
    {
        Refresh(RefreshAfterMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref loadedAtTick, 0);
        Interlocked.Exchange(ref attemptedAtTick, 0);
        Refresh(0);
    }

    private void Refresh(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastAttempt = Interlocked.Read(ref attemptedAtTick);
        if (lastAttempt != 0 && now - lastAttempt < RetryAfterAttemptMilliseconds)
        {
            return;
        }

        var lastLoad = Interlocked.Read(ref loadedAtTick);
        if (lastLoad != 0 && now - lastLoad < refreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetching, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref attemptedAtTick, now);
        work.Run("frame catalog refresh", async token =>
        {
            var catalog = await account.FrameCatalogAsync(token).ConfigureAwait(false);
            if (catalog is null)
            {
                return;
            }

            var next = new Dictionary<string, FrameStyle>(catalog.Frames.Length, StringComparer.Ordinal);
            for (var index = 0; index < catalog.Frames.Length; index++)
            {
                var style = FrameStyle.From(catalog.Frames[index]);
                next[style.Id] = style;
            }

            stylesById = next;
            patreonFrameIds = catalog.PatreonFrames ?? Array.Empty<string>();
            Interlocked.Exchange(ref loadedAtTick, Environment.TickCount64);
            Interlocked.Exchange(ref attemptedAtTick, 0);
            AepLog.Debug($"[FrameCatalog] loaded {next.Count} frames");
        }, () => Interlocked.Exchange(ref fetching, 0));
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
