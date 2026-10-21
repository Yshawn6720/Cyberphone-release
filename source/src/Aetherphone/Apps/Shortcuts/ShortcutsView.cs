namespace Aetherphone.Apps.Shortcuts;

internal enum ShortcutsRoute : byte
{
    Root,
    Editor,
    Appearance,
    Plugin,
    PluginPicker,
    Preview,
    CustomIcon,
}

internal enum ShortcutsTab : byte
{
    Library,
    Gallery,
    Plugins,
}

internal sealed class ShortcutsView
{
    public static readonly ShortcutsView Root = new(ShortcutsRoute.Root, string.Empty);

    public ShortcutsView(ShortcutsRoute route, string backTitle)
    {
        Route = route;
        BackTitle = backTitle;
    }

    public ShortcutsRoute Route { get; }
    public string BackTitle { get; }
}
