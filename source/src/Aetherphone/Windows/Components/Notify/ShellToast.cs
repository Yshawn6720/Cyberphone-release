using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ShellToast
{
    private static readonly ScreenToast Toast = new();
    private static Vector2 anchor;
    private static int secondaryClaimFrame = -1;

    public static void Show() => Show(Loc.T(L.Common.Copied));

    public static void Show(string text)
    {
        anchor = ImGui.GetMousePos();
        Toast.Show(text);
    }

    public static void Draw(Rect host, PhoneTheme theme)
    {
        if (ImGui.GetFrameCount() - secondaryClaimFrame <= 1)
        {
            return;
        }

        Toast.Draw(host, ScreenToastStyle.From(theme));
    }

    public static void DrawSecondary(Rect host, PhoneTheme theme)
    {
        if (!host.Contains(anchor))
        {
            return;
        }

        secondaryClaimFrame = ImGui.GetFrameCount();
        Toast.Draw(host, ScreenToastStyle.From(theme));
    }
}
