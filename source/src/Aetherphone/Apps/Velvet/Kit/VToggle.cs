using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VToggle
{
    public const float TrackWidth = 48f;
    public const float TrackHeight = 28f;

    public static bool Draw(string id, Rect row, bool value, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var trackMin = new Vector2(row.Max.X - TrackWidth * scale, row.Center.Y - TrackHeight * 0.5f * scale);
        var trackMax = new Vector2(row.Max.X, row.Center.Y + TrackHeight * 0.5f * scale);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var result = value;
        if (UiInteract.Click(row.Min, row.Max, hovered, false))
        {
            result = !value;
            UiFeedback.Play(result ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        Toggle.Draw(id, new Rect(trackMin, trackMax), value, theme, 1f, false);
        return result;
    }
}
