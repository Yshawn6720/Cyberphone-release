using System.Globalization;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.AppStore;

internal readonly record struct StoreUpdate(IPhoneApp App, LocString Highlight);

internal readonly record struct StoreRelease(string Version, string Date, IReadOnlyList<LocString> Highlights);

internal sealed class StoreIndex : IDisposable
{
    private static readonly Comparison<IPhoneApp> ByName = static (first, second) =>
        string.Compare(first.DisplayName, second.DisplayName, StringComparison.CurrentCultureIgnoreCase);

    private static readonly Comparison<IPhoneApp> ById = static (first, second) =>
        string.CompareOrdinal(first.Id, second.Id);

    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly AppInstaller installer;
    private readonly List<IPhoneApp>[] categories = new List<IPhoneApp>[AppStoreCatalog.Order.Length];
    private readonly List<IPhoneApp> fresh = new();
    private readonly List<IPhoneApp> results = new();
    private readonly List<IPhoneApp> looseResults = new();
    private readonly List<IPhoneApp> featurePool = new();
    private readonly List<StoreUpdate> updates = new();
    private readonly List<StoreUpdate> releaseSections = new();
    private readonly Dictionary<string, StoreRelease> releases = new(StringComparer.Ordinal);
    private bool[] availability = Array.Empty<bool>();
    private readonly Action<string> onInstalledChanged;
    private CultureInfo? culture;
    private DateTime day;
    private bool dirty = true;
    private bool resultsDirty = true;
    private string query = string.Empty;

    public StoreIndex(IReadOnlyList<IPhoneApp> apps, AppInstaller installer)
    {
        this.apps = apps;
        this.installer = installer;
        for (var index = 0; index < categories.Length; index++)
        {
            categories[index] = new List<IPhoneApp>();
        }

        LatestVersion = ChangelogData.Entries.Count > 0 ? ChangelogData.Entries[0].Version : string.Empty;
        onInstalledChanged = _ => dirty = true;
        installer.Changed += onInstalledChanged;
    }

    public string LatestVersion { get; }

    public IPhoneApp? Featured { get; private set; }

    public IReadOnlyList<IPhoneApp> Fresh => fresh;

    public IReadOnlyList<StoreUpdate> Updates => updates;

    public IReadOnlyList<IPhoneApp> Results => results;

    public IReadOnlyList<IPhoneApp> Category(StoreCategory category) => categories[(int)category];

    public bool TryRelease(string appId, out StoreRelease release) => releases.TryGetValue(appId, out release);

    public void Invalidate() => dirty = true;

    public void Refresh()
    {
        if (availability.Length != apps.Count)
        {
            availability = new bool[apps.Count];
            IndexReleases();
            dirty = true;
        }

        var today = DateTime.Now.Date;
        var stale = dirty || !ReferenceEquals(culture, Loc.Culture) || today != day;
        for (var index = 0; index < apps.Count; index++)
        {
            var available = apps[index].IsAvailable;
            if (availability[index] != available)
            {
                availability[index] = available;
                stale = true;
            }
        }

        if (!stale)
        {
            return;
        }

        dirty = false;
        culture = Loc.Culture;
        day = today;
        Rebuild(today);
        resultsDirty = true;
    }

    public void Search(ReadOnlySpan<char> text)
    {
        var trimmed = text.Trim();
        if (!resultsDirty && trimmed.SequenceEqual(query.AsSpan()))
        {
            return;
        }

        resultsDirty = false;
        query = trimmed.ToString();
        results.Clear();
        looseResults.Clear();
        if (query.Length == 0)
        {
            return;
        }

        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            if (!app.IsAvailable)
            {
                continue;
            }

            if (app.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                results.Add(app);
            }
            else if (AppStoreCatalog.Matches(app.Id, app.DisplayName, query))
            {
                looseResults.Add(app);
            }
        }

        results.Sort(ByName);
        looseResults.Sort(ByName);
        results.AddRange(looseResults);
    }

    public void Dispose() => installer.Changed -= onInstalledChanged;

    private void Rebuild(DateTime today)
    {
        for (var index = 0; index < categories.Length; index++)
        {
            categories[index].Clear();
        }

        fresh.Clear();
        featurePool.Clear();
        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            if (!app.IsAvailable)
            {
                continue;
            }

            categories[(int)AppStoreCatalog.For(app.Id).Category].Add(app);
            if (!AppInstaller.CanUninstall(app.Id))
            {
                continue;
            }

            featurePool.Add(app);
            if (!installer.IsInstalled(app.Id))
            {
                fresh.Add(app);
            }
        }

        for (var index = 0; index < categories.Length; index++)
        {
            categories[index].Sort(ByName);
        }

        fresh.Sort(ByName);
        featurePool.Sort(ById);
        Featured = featurePool.Count > 0 ? featurePool[(today.Year * 366 + today.DayOfYear) % featurePool.Count] : null;
        updates.Clear();
        for (var index = 0; index < releaseSections.Count; index++)
        {
            if (releaseSections[index].App.IsAvailable)
            {
                updates.Add(releaseSections[index]);
            }
        }
    }

    private void IndexReleases()
    {
        releaseSections.Clear();
        releases.Clear();
        var byTitle = new Dictionary<string, IPhoneApp>(StringComparer.Ordinal);
        for (var index = 0; index < apps.Count; index++)
        {
            if (AppStoreCatalog.TryFor(apps[index].Id, out var entry))
            {
                byTitle.TryAdd(entry.Name.Key, apps[index]);
            }
        }

        for (var entryIndex = 0; entryIndex < ChangelogData.Entries.Count; entryIndex++)
        {
            var release = ChangelogData.Entries[entryIndex];
            for (var sectionIndex = 0; sectionIndex < release.Sections.Count; sectionIndex++)
            {
                var section = release.Sections[sectionIndex];
                if (section.Highlights.Count == 0 || !byTitle.TryGetValue(section.Title.Key, out var app))
                {
                    continue;
                }

                if (entryIndex == 0)
                {
                    releaseSections.Add(new StoreUpdate(app, section.Highlights[0]));
                }

                releases.TryAdd(app.Id, new StoreRelease(release.Version, release.Date, section.Highlights));
            }
        }
    }
}
