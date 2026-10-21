using System.Reflection;
using System.Runtime.CompilerServices;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AppStoreCatalogTests
{
    private const int MinimumCategorySize = 3;
    private const int MaximumCategorySize = 8;

    private static readonly string[] PermanentApps =
    {
        "announcements", "appstore", "camera", "messages", "notifications", "photos", "settings",
    };

    [Fact]
    public void EveryRegisteredApp_HasExactlyOneCatalogEntry()
    {
        var registered = RegisteredAppIds();
        var listed = new HashSet<string>(AppStoreCatalog.AppIds, StringComparer.Ordinal);

        Assert.True(registered.Count > 0, "No IPhoneApp types were discovered.");
        for (var index = 0; index < registered.Count; index++)
        {
            Assert.True(listed.Contains(registered[index]), $"'{registered[index]}' has no App Store entry");
        }

        Assert.Equal(registered.Count, listed.Count);
    }

    [Fact]
    public void EveryCategoryIsInTheOrderAndHasASensibleSize()
    {
        var sizes = new int[AppStoreCatalog.Order.Length];
        var ids = AppStoreCatalog.AppIds;
        for (var index = 0; index < ids.Length; index++)
        {
            var category = AppStoreCatalog.For(ids[index]).Category;
            var position = Array.IndexOf(AppStoreCatalog.Order, category);
            Assert.True(position >= 0, $"'{ids[index]}' sits in {category}, which the store never shows");
            sizes[position]++;
        }

        for (var index = 0; index < sizes.Length; index++)
        {
            Assert.InRange(sizes[index], MinimumCategorySize, MaximumCategorySize);
        }

        Assert.Equal(Enum.GetValues<StoreCategory>().Length, AppStoreCatalog.Order.Length);
    }

    [Fact]
    public void OnlySystemAppsArePermanent()
    {
        var ids = AppStoreCatalog.AppIds;
        var permanent = new List<string>();
        for (var index = 0; index < ids.Length; index++)
        {
            if (!HomeLayoutService.CanUninstall(ids[index]))
            {
                permanent.Add(ids[index]);
            }
        }

        permanent.Sort(StringComparer.Ordinal);
        Assert.Equal(PermanentApps, permanent);
    }

    [Theory]
    [InlineData("chirper")]
    [InlineData("message")]
    [InlineData("clock")]
    [InlineData("feedback")]
    [InlineData("wallet")]
    public void RemovableApp_CanBeUninstalledAndStaysGoneAfterAReload(string appId)
    {
        var apps = new List<IPhoneApp> { new FakeApp("settings"), new FakeApp(appId) };
        var configuration = new FakeHomeConfiguration();
        var first = new HomeLayoutService(apps, new WidgetRegistry(new List<IHomeWidget>(), apps),
            new FakeShortcutSource(), configuration);

        Assert.True(first.Uninstall(appId));

        var reloaded = new HomeLayoutService(apps, new WidgetRegistry(new List<IHomeWidget>(), apps),
            new FakeShortcutSource(), configuration);
        Assert.False(reloaded.IsInstalled(appId));
    }

    private static List<string> RegisteredAppIds()
    {
        var ids = new List<string>();
        var types = typeof(IPhoneApp).Assembly.GetTypes();
        for (var index = 0; index < types.Length; index++)
        {
            var type = types[index];
            if (type.IsAbstract || type.IsInterface || !typeof(IPhoneApp).IsAssignableFrom(type) ||
                type.Namespace is null || !type.Namespace.StartsWith("Aetherphone.Apps", StringComparison.Ordinal))
            {
                continue;
            }

            var instance = (IPhoneApp)RuntimeHelpers.GetUninitializedObject(type);
            ids.Add(instance.Id);
        }

        ids.Sort(StringComparer.Ordinal);
        return ids;
    }
}
