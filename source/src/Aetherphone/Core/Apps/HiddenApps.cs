using System.Collections.Frozen;
using Aetherphone.Core.Home;
using Dalamud.Game;

namespace Aetherphone.Core.Apps;

// Cyberphone fork policy: the upstream trademark terms forbid forks from reaching the official Aethernet
// service. Apps that only work against it are never registered (so no home tile, dock slot, App Store row,
// Spotlight hit, widget, notification route, command or install gate can reach them), and Aethernet-only
// entry points inside kept apps check AethernetEnabled. The Core/Aethernet code stays compiled but dormant.
internal static class HiddenApps
{
    public static readonly FrozenSet<string> Ids = new[]
    {
        "aethergram", "announcements", "casino", "chirper", "coin", "feedback",
        "message", "muster", "polls", "strats", "velvet", "yellowpages",
    }.ToFrozenSet(StringComparer.Ordinal);

    // Faloop tracks only the global data centers, so the Traditional Chinese client (which reaches only the
    // Taiwan data center) gets no hunt data and Faloop answers its data center with a 400.
    private static readonly FrozenSet<string> FaloopIds = new[] { "hunts" }.ToFrozenSet(StringComparer.Ordinal);

    public static bool AethernetEnabled => false;

    public static bool FaloopReachable { get; private set; } = true;

    public static void ApplyClient(ClientLanguage language) =>
        FaloopReachable = language is not (ClientLanguage.TraditionalChinese or ClientLanguage.ChineseTraditional);

    public static bool Contains(string appId) => Ids.Contains(appId) || (!FaloopReachable && FaloopIds.Contains(appId));

    public static void Prune(List<IPhoneApp> apps)
    {
        for (var index = apps.Count - 1; index >= 0; index--)
        {
            var app = apps[index];
            if (!Contains(app.Id))
            {
                continue;
            }

            apps.RemoveAt(index);
            app.Dispose();
        }
    }

    public static WidgetRegistry WithoutHiddenWidgets(WidgetRegistry registry, IReadOnlyList<IPhoneApp> apps)
    {
        var all = registry.All;
        var kept = new List<IHomeWidget>(all.Count);
        for (var index = 0; index < all.Count; index++)
        {
            var widget = all[index];
            if (Contains(widget.AppId))
            {
                widget.Dispose();
                continue;
            }

            kept.Add(widget);
        }

        return kept.Count == all.Count ? registry : new WidgetRegistry(kept, apps);
    }
}
