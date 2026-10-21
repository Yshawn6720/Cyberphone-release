using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class HomeIndicator
{
    private const float Width = 112f;
    private const float Height = 5f;
    private const float BottomInset = 14f;
    private const float RestingAlpha = 0.55f;

    public static Rect Bounds(Rect screen, float scale)
    {
        var width = Width * scale;
        var height = Height * scale;
        var center = new Vector2(screen.Center.X, screen.Max.Y - BottomInset * scale);
        return new Rect(new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f),
            new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f));
    }

    public static void Draw(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, bool actionable)
    {
        var color = actionable ? theme.TextStrong : Palette.WithAlpha(theme.TextStrong, RestingAlpha);
        drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(color), bounds.Height * 0.5f);
    }
}
