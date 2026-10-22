using System.Runtime.CompilerServices;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Commands;
using Aetherphone.Core.Home;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Shell.Spotlight;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HiddenAppsTests
{
    private static readonly string[] AethernetApps =
    {
        "aethergram", "casino", "chirper", "coin", "feedback",
        "message", "muster", "polls", "strats", "velvet", "yellowpages",
    };

    private static readonly string[] LocalApps = { "messages", "character", "photos", "wallet", "notifications" };

    [Fact]
    public void Ids_AreExactlyTheAethernetApps()
    {
        Assert.Equal(AethernetApps.Length, HiddenApps.Ids.Count);
        for (var index = 0; index < AethernetApps.Length; index++)
        {
            Assert.True(HiddenApps.Contains(AethernetApps[index]), $"'{AethernetApps[index]}' should be hidden");
        }

        for (var index = 0; index < LocalApps.Length; index++)
        {
            Assert.False(HiddenApps.Contains(LocalApps[index]), $"local app '{LocalApps[index]}' must stay visible");
        }

        Assert.False(HiddenApps.AethernetEnabled);
    }

    [Fact]
    public void EveryHiddenId_NamesARealApp()
    {
        var shipped = ShippedAppIds();
        foreach (var id in HiddenApps.Ids)
        {
            Assert.True(shipped.Contains(id), $"hidden id '{id}' matches no IPhoneApp; a typo would leave it visible");
        }
    }

    [Fact]
    public void Prune_DropsAndDisposesHiddenApps_KeepingVisibleOrder()
    {
        var hiddenMessage = new TrackedApp("message");
        var hiddenChirper = new TrackedApp("chirper");
        var hiddenStrats = new TrackedApp("strats");
        var linkpearl = new TrackedApp("messages");
        var camera = new TrackedApp("camera");
        var apps = new List<IPhoneApp> { hiddenMessage, linkpearl, hiddenChirper, camera, hiddenStrats };

        HiddenApps.Prune(apps);

        Assert.Equal(new[] { "messages", "camera" }, Ids(apps));
        Assert.True(hiddenMessage.Disposed && hiddenChirper.Disposed && hiddenStrats.Disposed);
        Assert.False(linkpearl.Disposed || camera.Disposed);
    }

    [Fact]
    public void WithoutHiddenWidgets_DropsAndDisposesWidgetsOfHiddenApps()
    {
        var apps = VisibleApps();
        var chats = new TrackedWidget("message.chats", "message");
        var coin = new TrackedWidget("coin.balance", "coin");
        var tells = new TrackedWidget("messages.tells", "messages");
        var registry = new WidgetRegistry(new IHomeWidget[] { chats, tells, coin }, apps);

        var filtered = HiddenApps.WithoutHiddenWidgets(registry, apps);

        Assert.Single(filtered.All);
        Assert.Same(tells, filtered.All[0]);
        Assert.False(filtered.TryGet("message.chats", out _));
        Assert.True(chats.Disposed && coin.Disposed);
        Assert.False(tells.Disposed);
    }

    [Fact]
    public void SavedConfigListingHiddenApps_LoadsWithoutThemOnHomeOrDock()
    {
        var apps = VisibleApps();
        var configuration = SavedWith(dock: new[] { "message", "messages" },
            "chirper", "messages", "announcements", "camera", "strats", "photos");

        var layout = BuildLayout(apps, configuration);

        Assert.Equal(new[] { "messages" }, DockIds(layout));
        var onHome = HomeIds(layout);
        for (var index = 0; index < AethernetApps.Length; index++)
        {
            Assert.DoesNotContain(AethernetApps[index], onHome);
        }

        Assert.Contains("camera", onHome);
        Assert.Contains("photos", onHome);
    }

    [Fact]
    public void Installer_NeverReportsAHiddenAppInstalled_EvenWhenTheSaveListsIt()
    {
        var apps = VisibleApps();
        var installer = new AppInstaller();
        installer.Bind(BuildLayout(apps, SavedWith(dock: null, "message", "muster", "messages", "camera")));

        Assert.False(installer.IsInstalled("message"));
        Assert.False(installer.Gate("message").Open);
        Assert.False(installer.IsInstalled("muster"));
        Assert.False(installer.Install("chirper"));
        Assert.True(installer.IsInstalled("messages"));
        Assert.True(installer.Gate("camera").Open);
    }

    [Fact]
    public void AethernetCallEntries_AreNotListed()
    {
        Assert.DoesNotContain(SpotlightActionKind.Calls, SpotlightActions.All);
        Assert.Contains(SpotlightActionKind.DoNotDisturb, SpotlightActions.All);

        var entries = PhoneCommandCatalog.Entries;
        for (var index = 0; index < entries.Length; index++)
        {
            Assert.DoesNotContain(" call ", entries[index].Syntax, StringComparison.Ordinal);
        }

        Assert.Contains(entries, entry => entry.Syntax.Contains(" tell ", StringComparison.Ordinal));
    }

    [Fact]
    public void AethernetNameplateStatuses_AreHidden_LocalOnesStay()
    {
        Assert.True(NameplateStatusCatalog.IsHidden(NameplateStatus.Handle));
        Assert.True(NameplateStatusCatalog.IsHidden(NameplateStatus.Chirper));
        Assert.True(NameplateStatusCatalog.IsHidden(NameplateStatus.InCall));
        Assert.True(NameplateStatusCatalog.IsHidden(NameplateStatus.Jam));
        Assert.False(NameplateStatusCatalog.IsHidden(NameplateStatus.NowPlaying));
        Assert.False(NameplateStatusCatalog.IsHidden(NameplateStatus.Games));
        Assert.False(NameplateStatusCatalog.IsHidden(NameplateStatus.DoNotDisturb));
        Assert.False(NameplateStatusCatalog.IsHidden(NameplateStatus.Custom));
    }

    [Fact]
    public void MinimizedCoinPart_StaysOffEvenWhenTheSaveEnablesIt_RelayCallsStay()
    {
        var saved = new MinimizedLayout();
        saved.Items.Add(new MinimizedLayoutItem { PartId = MinimizedParts.Id(MinimizedPart.Coin), Enabled = true });
        saved.Items.Add(new MinimizedLayoutItem { PartId = MinimizedParts.Id(MinimizedPart.Calls), Enabled = true });
        saved.Items.Add(new MinimizedLayoutItem { PartId = MinimizedParts.Id(MinimizedPart.Gil), Enabled = true });
        var service = new MinimizedLayoutService(new MinimizedConfiguration { MinimizedLayout = saved });

        Assert.False(service.IsEnabled(MinimizedPart.Coin));
        Assert.True(service.IsEnabled(MinimizedPart.Calls));
        Assert.True(service.IsEnabled(MinimizedPart.Gil));
        Assert.True(MinimizedParts.EnabledByDefault(MinimizedPart.Calls));
    }

    private static List<IPhoneApp> VisibleApps()
    {
        var apps = new List<IPhoneApp>();
        var ids = new[] { "messages", "camera", "photos", "settings", "appstore", "notifications", "chirper" };
        for (var index = 0; index < ids.Length; index++)
        {
            apps.Add(new TrackedApp(ids[index]));
        }

        HiddenApps.Prune(apps);
        return apps;
    }

    private static HomeLayoutService BuildLayout(List<IPhoneApp> apps, FakeHomeConfiguration configuration) =>
        new(apps, new WidgetRegistry(Array.Empty<IHomeWidget>(), apps), new FakeShortcutSource(), configuration);

    private static FakeHomeConfiguration SavedWith(string[]? dock, params string[] appIds)
    {
        var page = new HomePage();
        for (var index = 0; index < appIds.Length; index++)
        {
            page.Items.Add(new HomeItem { Kind = "app", AppId = appIds[index] });
        }

        return new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = dock is null ? new List<string>() : new List<string>(dock),
                Pages = new List<HomePage> { page },
                Installed = new List<string>(appIds),
            },
        };
    }

    private static List<string> DockIds(HomeLayoutService layout)
    {
        var ids = new List<string>();
        var dock = layout.Dock;
        for (var index = 0; index < dock.Count; index++)
        {
            if (dock[index].App is { } app)
            {
                ids.Add(app.Id);
            }
        }

        return ids;
    }

    private static List<string> HomeIds(HomeLayoutService layout)
    {
        var ids = new List<string>();
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].App is { } app)
                {
                    ids.Add(app.Id);
                }

                for (var member = 0; member < tiles[index].Members.Count; member++)
                {
                    if (tiles[index].Members[member].App is { } inner)
                    {
                        ids.Add(inner.Id);
                    }
                }
            }
        }

        return ids;
    }

    private static string[] Ids(List<IPhoneApp> apps)
    {
        var ids = new string[apps.Count];
        for (var index = 0; index < apps.Count; index++)
        {
            ids[index] = apps[index].Id;
        }

        return ids;
    }

    private static HashSet<string> ShippedAppIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var types = typeof(IPhoneApp).Assembly.GetTypes();
        for (var index = 0; index < types.Length; index++)
        {
            var type = types[index];
            if (type.IsAbstract || type.IsInterface || !typeof(IPhoneApp).IsAssignableFrom(type) ||
                type.Namespace is null || !type.Namespace.StartsWith("Aetherphone.Apps", StringComparison.Ordinal))
            {
                continue;
            }

            ids.Add(((IPhoneApp)RuntimeHelpers.GetUninitializedObject(type)).Id);
        }

        return ids;
    }

    private sealed class TrackedApp : IPhoneApp
    {
        public TrackedApp(string id) => Id = id;

        public string Id { get; }
        public string DisplayName => Id;
        public string Glyph => string.Empty;
        public int BadgeCount => 0;
        public bool IsAvailable => true;
        public bool Disposed { get; private set; }

        public void OnOpened() { }
        public void OnClosed() { }
        public void Draw(in PhoneContext context) { }
        public void Dispose() => Disposed = true;
    }

    private sealed class TrackedWidget : IHomeWidget
    {
        public TrackedWidget(string id, string appId)
        {
            Id = id;
            AppId = appId;
        }

        public string Id { get; }
        public string DisplayName => Id;
        public string Description => Id;
        public string AppId { get; }
        public WidgetSizeSet Sizes => WidgetSizeSet.Small;
        public bool Disposed { get; private set; }

        public void Draw(in WidgetContext context) { }
        public void Dispose() => Disposed = true;
    }

    private sealed class MinimizedConfiguration : IMinimizedConfiguration
    {
        public MinimizedLayout? MinimizedLayout { get; set; }

        public void Save() { }
    }
}
