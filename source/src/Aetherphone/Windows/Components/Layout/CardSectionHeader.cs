using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CardSectionHeader
{
    public const float HeightUnits = 40f;

    public static float Draw(ImDrawListPtr drawList, Vector2 origin, float width, string title, Vector4 ink,
        float trailingReserve = 0f)
    {
        var height = HeightUnits * UiScale.Current;
        var fitted = Typography.FitText(title, MathF.Max(1f, width - trailingReserve), TextStyles.Title3);
        var size = Typography.Measure(fitted, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(origin.X, origin.Y + (height - size.Y) * 0.5f), fitted, ink,
            TextStyles.Title3);
        return height;
    }

    public static void Flow(string title, Vector4 ink)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Draw(ImGui.GetWindowDrawList(), origin, width, title, ink);
        ImGui.Dummy(new Vector2(width, height));
    }
}
