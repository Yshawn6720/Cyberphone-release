using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell.Spotlight;

internal enum SpotlightActionKind : byte
{
    DoNotDisturb,
    SilentMode,
    Calls,
    ScrollWhileIdle,
    LockPosition,
    AppearanceLight,
    AppearanceDark,
    AppearanceAuto,
    TakePhoto,
    NewNote,
}

internal static class SpotlightActions
{
    private const string CallsAppId = "message";

    public static readonly SpotlightActionKind[] All = Array.FindAll(new[]
    {
        SpotlightActionKind.DoNotDisturb,
        SpotlightActionKind.SilentMode,
        SpotlightActionKind.Calls,
        SpotlightActionKind.ScrollWhileIdle,
        SpotlightActionKind.LockPosition,
        SpotlightActionKind.AppearanceLight,
        SpotlightActionKind.AppearanceDark,
        SpotlightActionKind.AppearanceAuto,
        SpotlightActionKind.TakePhoto,
        SpotlightActionKind.NewNote,
    }, kind => kind != SpotlightActionKind.Calls || !HiddenApps.Contains(CallsAppId));

    public static LocString Label(SpotlightActionKind kind) => kind switch
    {
        SpotlightActionKind.DoNotDisturb => L.Settings.DoNotDisturb,
        SpotlightActionKind.SilentMode => L.Settings.SilentMode,
        SpotlightActionKind.Calls => L.Phone.Calls,
        SpotlightActionKind.ScrollWhileIdle => L.Settings.ScrollWhileIdle,
        SpotlightActionKind.LockPosition => L.ControlCenter.LockPosition,
        SpotlightActionKind.AppearanceLight => L.Settings.ThemeLight,
        SpotlightActionKind.AppearanceDark => L.Settings.ThemeDark,
        SpotlightActionKind.AppearanceAuto => L.Settings.ThemeAuto,
        SpotlightActionKind.TakePhoto => L.Spotlight.TakePhoto,
        _ => L.Notes.NewNote,
    };

    public static FontAwesomeIcon Icon(SpotlightActionKind kind) => kind switch
    {
        SpotlightActionKind.DoNotDisturb => FontAwesomeIcon.Moon,
        SpotlightActionKind.SilentMode => FontAwesomeIcon.BellSlash,
        SpotlightActionKind.Calls => FontAwesomeIcon.Phone,
        SpotlightActionKind.ScrollWhileIdle => FontAwesomeIcon.HandPointUp,
        SpotlightActionKind.LockPosition => FontAwesomeIcon.Thumbtack,
        SpotlightActionKind.AppearanceLight => FontAwesomeIcon.Sun,
        SpotlightActionKind.AppearanceDark => FontAwesomeIcon.Moon,
        SpotlightActionKind.AppearanceAuto => FontAwesomeIcon.Adjust,
        SpotlightActionKind.TakePhoto => FontAwesomeIcon.Camera,
        _ => FontAwesomeIcon.PenAlt,
    };

    public static bool IsAppearance(SpotlightActionKind kind) =>
        kind is SpotlightActionKind.AppearanceLight or SpotlightActionKind.AppearanceDark
            or SpotlightActionKind.AppearanceAuto;

    public static string Subtitle(SpotlightActionKind kind, Configuration configuration) => kind switch
    {
        SpotlightActionKind.DoNotDisturb => StateText(configuration.DoNotDisturb),
        SpotlightActionKind.SilentMode => StateText(configuration.SilentMode),
        SpotlightActionKind.Calls => StateText(configuration.CallsEnabled),
        SpotlightActionKind.ScrollWhileIdle => StateText(configuration.ScrollWhileIdle),
        SpotlightActionKind.LockPosition => StateText(configuration.LockPosition),
        SpotlightActionKind.AppearanceLight => AppearanceText(configuration, ThemeMode.Light),
        SpotlightActionKind.AppearanceDark => AppearanceText(configuration, ThemeMode.Dark),
        SpotlightActionKind.AppearanceAuto => AppearanceText(configuration, ThemeMode.Auto),
        _ => string.Empty,
    };

    public static void Run(SpotlightActionKind kind, Configuration configuration, ThemeProvider themes, CallHub calls,
        ISpotlightNotes? notes, INavigator navigation)
    {
        switch (kind)
        {
            case SpotlightActionKind.DoNotDisturb:
                configuration.DoNotDisturb = !configuration.DoNotDisturb;
                configuration.Save();
                break;
            case SpotlightActionKind.SilentMode:
                configuration.SilentMode = !configuration.SilentMode;
                configuration.Save();
                break;
            case SpotlightActionKind.Calls:
                calls.SetEnabled(!configuration.CallsEnabled);
                break;
            case SpotlightActionKind.ScrollWhileIdle:
                configuration.ScrollWhileIdle = !configuration.ScrollWhileIdle;
                configuration.Save();
                break;
            case SpotlightActionKind.LockPosition:
                configuration.LockPosition = !configuration.LockPosition;
                configuration.Save();
                break;
            case SpotlightActionKind.AppearanceLight:
                ApplyTheme(configuration, themes, ThemeMode.Light);
                break;
            case SpotlightActionKind.AppearanceDark:
                ApplyTheme(configuration, themes, ThemeMode.Dark);
                break;
            case SpotlightActionKind.AppearanceAuto:
                ApplyTheme(configuration, themes, ThemeMode.Auto);
                break;
            case SpotlightActionKind.TakePhoto:
                navigation.Open("camera");
                break;
            case SpotlightActionKind.NewNote:
                notes?.RequestNewNote();
                navigation.Open("notes");
                break;
        }
    }

    private static void ApplyTheme(Configuration configuration, ThemeProvider themes, ThemeMode mode)
    {
        configuration.ThemeMode = mode;
        themes.Apply(configuration);
        configuration.Save();
    }

    private static string StateText(bool on) => Loc.T(on ? L.Common.On : L.Common.Off);

    private static string AppearanceText(Configuration configuration, ThemeMode mode)
    {
        var theme = Loc.T(L.Settings.Theme);
        return configuration.ThemeMode == mode ? $"{theme}, {Loc.T(L.Common.On)}" : theme;
    }
}
