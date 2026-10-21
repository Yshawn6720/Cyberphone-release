using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AwayBadge
{
    public const float Height = 24f;

    public static float Draw(ImDrawListPtr drawList, Vector2 rightAnchor, AppSkin ui, string label, float scale)
    {
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var height = Height * scale;
        var max = new Vector2(rightAnchor.X, rightAnchor.Y + height);
        var min = new Vector2(max.X - labelSize.X - 26f * scale, rightAnchor.Y);
        var rounding = height * 0.5f;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.30f)), 1f * scale);
        var dotCenter = new Vector2(min.X + 10f * scale, (min.Y + max.Y) * 0.5f);
        drawList.AddCircleFilled(dotCenter, 3f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.7f)), 12);
        Typography.Draw(drawList, new Vector2(dotCenter.X + 7f * scale, min.Y + 4f * scale), label, ui.MutedInk,
            TextStyles.Caption2);
        return max.X - min.X;
    }
}
