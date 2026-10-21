using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ActivityBadge
{
    private static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);

    public static void Draw(Vector2 center, int count, PhoneTheme theme, float scale)
    {
        if (count <= 0)
        {
            return;
        }

        var label = count > 9 ? "9+" : count.ToString(Loc.Culture);
        var radius = count > 9 ? 9f * scale : 7f * scale;
        ImGui.GetWindowDrawList().AddCircleFilled(center, radius, ImGui.GetColorU32(theme.Danger), 16);
        Typography.DrawCentered(center, label, Ink, 0.62f, FontWeight.SemiBold);
    }
}
