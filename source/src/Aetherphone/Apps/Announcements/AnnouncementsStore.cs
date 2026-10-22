using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Announcements;

internal sealed class AnnouncementsStore : IDisposable
{
    public const string AppId = "announcements";
    private const string RelayDocument = "announcements";
    private const string RelayCacheFileName = "relay_announcements.json";

    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly AethernetSession session;
    private readonly AnnouncementsClient client;
    private readonly RelayContentFeed<AnnouncementPage> feed;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly StoreWork work = new StoreWork("Announcements");

    private volatile AnnouncementDto[] announcements = Array.Empty<AnnouncementDto>();
    private volatile string? announcementsCursor;
    private volatile bool loadingMore;
    private volatile bool pagedDeeper;
    private volatile bool loading;
    private volatile bool loadedOnce;
    private volatile AepFailureBox? failureBox;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence cadence;
    private readonly HashSet<string> readThisVisit = new(StringComparer.Ordinal);
    private long visitSeenUnix = long.MaxValue;
    private AnnouncementPage? relayPage;

    public AnnouncementsStore(AethernetSession session, AnnouncementsClient client, HttpService http,
        NotificationService notifications, Configuration configuration, PhoneVisibility visibility,
        RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.signals = signals;
        feed = new RelayContentFeed<AnnouncementPage>(http, configuration, RelayDocument,
            AethernetJsonContext.Default.AnnouncementPage,
            Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, RelayCacheFileName));
        cadence = new PollCadence(visibility, BackgroundRefreshInterval, BackgroundRefreshInterval, signals);
        signals.AnnouncementsPinged += cadence.RequestImmediate;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    private void OnRealtimeConnected(bool active)
    {
        if (active)
        {
            cadence.RequestAfterReconnect();
        }
    }

    public static bool RelayMode => !HiddenApps.AethernetEnabled;

    public bool Available => RelayMode ? feed.IsConfigured : AppAvailability.IsEnabled(AppId);

    public bool CanRead => RelayMode ? feed.IsConfigured : session.IsSignedIn;

    public bool PushCovered => signals.RealtimeActive;

    public AnnouncementDto[] Announcements => announcements;

    public bool Loading => loading;

    public bool LoadingMore => loadingMore;

    public bool HasMore => announcementsCursor is not null;

    public bool LoadedOnce => loadedOnce;

    public bool Failed => failureBox is not null;

    public AepFailure Failure => failureBox?.Failure ?? AepFailure.None;

    public int UnreadCount
    {
        get
        {
            if (!CanRead)
            {
                return 0;
            }

            var snapshot = announcements;
            var seenUnix = configuration.AnnouncementsSeenUnix;
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].CreatedAtUnix > seenUnix)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool IsUnread(AnnouncementDto announcement) =>
        announcement.CreatedAtUnix > Volatile.Read(ref visitSeenUnix) && !readThisVisit.Contains(announcement.Id);

    public void BeginVisit()
    {
        readThisVisit.Clear();
        Volatile.Write(ref visitSeenUnix,
            configuration.AnnouncementsInitialized ? configuration.AnnouncementsSeenUnix : long.MaxValue);
    }

    public void MarkRead(string announcementId) => readThisVisit.Add(announcementId);

    public void MarkAllRead()
    {
        var newest = NewestUnix(announcements);
        if (newest > Volatile.Read(ref visitSeenUnix))
        {
            Volatile.Write(ref visitSeenUnix, newest);
        }

        readThisVisit.Clear();
        MarkAllSeen();
    }

    public void Refresh()
    {
        if (!CanRead || loading)
        {
            return;
        }

        loading = true;
        if (RelayMode)
        {
            work.Run("announcements relay", LoadFromRelayAsync, () => loading = false);
            return;
        }

        work.Run("announcements refresh", async token =>
        {
            var reported = AepFailure.None;
            var page = await client.ListAsync(null, token, failure => reported = failure).ConfigureAwait(false);
            if (page is null)
            {
                failureBox = new AepFailureBox(reported.Failed
                    ? reported
                    : AepFailure.Transport(AepFailureKind.Offline));
                AepLog.Warning($"Announcements failed to load: {failureBox.Failure.Describe()}");
                return;
            }

            failureBox = null;

            if (pagedDeeper)
            {
                announcements = IdentifiedMerge.MergeById(announcements, page.Items, ByNewestFirst);
            }
            else
            {
                announcements = page.Items;
                announcementsCursor = page.NextCursor;
            }

            loadedOnce = true;
            Announce(page.Items);
        }, () => loading = false);
    }

    private async Task LoadFromRelayAsync(CancellationToken token)
    {
        var result = await feed.LoadAsync(token).ConfigureAwait(false);
        if (result.Status is RelayContentStatus.Failed or RelayContentStatus.Unsupported)
        {
            failureBox = new AepFailureBox(result.Failure);
            AepLog.Warning($"Announcements failed to load from the relay: {result.Failure.Describe()}");
        }
        else
        {
            failureBox = null;
        }

        if (result.Value is not { } page || ReferenceEquals(page, relayPage))
        {
            return;
        }

        relayPage = page;
        var items = Readable(page.Items);
        announcements = items;
        announcementsCursor = null;
        loadedOnce = true;
        Announce(items);
    }

    private static AnnouncementDto[] Readable(AnnouncementDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return Array.Empty<AnnouncementDto>();
        }

        var kept = new List<AnnouncementDto>(items.Length);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item is not null && !string.IsNullOrEmpty(item.Id) && item.Title is not null && item.Body is not null)
            {
                kept.Add(item);
            }
        }

        var readable = kept.ToArray();
        Array.Sort(readable, ByNewestFirst);
        return readable;
    }

    public void LoadMore()
    {
        var cursor = announcementsCursor;
        if (!session.IsSignedIn || cursor is null || loadingMore || loading)
        {
            return;
        }

        loadingMore = true;
        pagedDeeper = true;
        work.Run("announcements more", async token =>
        {
            var page = await client.ListAsync(cursor, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            announcements = IdentifiedMerge.MergeById(announcements, page.Items, ByNewestFirst);
            announcementsCursor = page.NextCursor;
        }, () => loadingMore = false);
    }

    private static int ByNewestFirst(AnnouncementDto left, AnnouncementDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public void MarkAllSeen()
    {
        var newest = NewestUnix(announcements);
        if (newest <= configuration.AnnouncementsSeenUnix)
        {
            return;
        }

        configuration.AnnouncementsSeenUnix = newest;
        configuration.Save();
    }

    private void Announce(AnnouncementDto[] items)
    {
        var newest = NewestUnix(items);
        if (!configuration.AnnouncementsInitialized)
        {
            configuration.AnnouncementsInitialized = true;
            configuration.AnnouncementsNotifiedUnix = newest;
            configuration.AnnouncementsSeenUnix = newest;
            configuration.Save();
            Volatile.Write(ref visitSeenUnix, newest);
            return;
        }

        var notifiedUnix = configuration.AnnouncementsNotifiedUnix;
        if (newest <= notifiedUnix)
        {
            return;
        }

        if (!RelayMode)
        {
            var accent = AppAccents.For(AppId);
            for (var index = items.Length - 1; index >= 0; index--)
            {
                var announcement = items[index];
                if (announcement.CreatedAtUnix <= notifiedUnix)
                {
                    continue;
                }

                var text = AnnouncementText.For(announcement);
                notifications.Notify(new PhoneNotification(AppId, text.Title, text.Body, DateTime.Now, accent,
                    announcement.Id));
            }
        }

        configuration.AnnouncementsNotifiedUnix = newest;
        configuration.Save();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        if (!cadence.Due(DateTime.UtcNow))
        {
            return;
        }

        Refresh();
    }

    private static long NewestUnix(AnnouncementDto[] items)
    {
        var newest = 0L;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].CreatedAtUnix > newest)
            {
                newest = items[index].CreatedAtUnix;
            }
        }

        return newest;
    }

    public void Dispose()
    {
        signals.AnnouncementsPinged -= cadence.RequestImmediate;
        signals.ConnectedChanged -= OnRealtimeConnected;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }
}
