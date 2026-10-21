using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class GifBadge
{
    public static void Draw(ImDrawListPtr drawList, Rect rect)
    {
        var scale = UiScale.Current;
        var size = Typography.Measure("GIF", TextStyles.FootnoteEmphasized);
        var padding = new Vector2(7f * scale, 3f * scale);
        var min = new Vector2(rect.Min.X + 10f * scale, rect.Max.Y - 10f * scale - size.Y - padding.Y * 2f);
        var max = min + size + padding * 2f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)), (max.Y - min.Y) * 0.5f);
        Typography.Draw(drawList, min + padding, "GIF", new Vector4(1f, 1f, 1f, 0.95f), TextStyles.FootnoteEmphasized);
    }
}
