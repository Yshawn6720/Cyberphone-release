using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings;

internal static class SettingsReorder
{
    public static bool Button(Vector2 center, float radius, FontAwesomeIcon icon, PhoneTheme theme, bool enabled)
    {
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && UiInteract.Hover(min, max);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.10f)), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = enabled ? theme.TextMuted : Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * 0.25f);
        ProgressRing.CenterIcon(drawList, center, icon, ink, radius);
        return enabled && UiInteract.Click(min, max, hovered);
    }
}
