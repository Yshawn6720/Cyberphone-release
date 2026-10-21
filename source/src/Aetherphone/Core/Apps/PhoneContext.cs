using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Apps;

internal readonly struct PhoneContext
{
    public readonly Rect Content;
    public readonly PhoneTheme Theme;
    public readonly INavigator Navigation;

    public PhoneContext(Rect content, PhoneTheme theme, INavigator navigation)
    {
        Content = content;
        Theme = theme;
        Navigation = navigation;
    }
}
