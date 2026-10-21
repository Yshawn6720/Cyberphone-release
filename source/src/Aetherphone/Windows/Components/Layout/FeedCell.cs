using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct FeedCellScope
{
    public readonly Rect Bounds;
    public readonly bool Hovered;
    public readonly bool Tapped;

    public FeedCellScope(Rect bounds, bool hovered, bool tapped)
    {
        Bounds = bounds;
        Hovered = hovered;
        Tapped = tapped;
    }
}

internal static class FeedCell
{
    public const float PadX = 16f;
    public const float PadTop = 11f;
    public const float PadBottom = 6f;

    public static FeedCellScope Begin(ImDrawListPtr drawList, float height, Vector4 hoverWash, bool interactive = true)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = interactive && UiInteract.Hover(origin, max);
        if (hovered)
        {
            drawList.AddRectFilled(origin, max, ImGui.GetColorU32(hoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tapped = interactive && UiInteract.Click(origin, max, hovered);
        return new FeedCellScope(new Rect(origin, max), hovered, tapped);
    }

    public static void End(ImDrawListPtr drawList, in FeedCellScope cell, Vector4 hairline, bool separator = true)
    {
        if (separator)
        {
            Hairline(drawList, cell.Bounds.Min.X, cell.Bounds.Max.X, cell.Bounds.Max.Y, hairline);
        }

        ImGui.SetCursorScreenPos(cell.Bounds.Min);
        ImGui.Dummy(new Vector2(cell.Bounds.Width, cell.Bounds.Height));
    }

    public static void Hairline(ImDrawListPtr drawList, float left, float right, float y, Vector4 color) =>
        drawList.AddLine(new Vector2(left, y - 0.5f), new Vector2(right, y - 0.5f), ImGui.GetColorU32(color), 1f);
}
