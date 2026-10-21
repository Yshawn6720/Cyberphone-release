using Aetherphone.Core;

namespace Aetherphone.Windows.Components;

internal static class TextButton
{
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static float Width(string label, float scale) => Button.WidthFor(label, ButtonSize.Small);

    public static bool Draw(Vector2 center, string label, Vector4 color, float scale)
    {
        var size = new Vector2(Width(label, scale), Button.SmallHeight * UiScale.Current);
        var rect = new Rect(center - size * 0.5f, center + size * 0.5f);
        var ink = new ControlInk(color with { W = 1f }, White, color, color);
        return Button.Draw(rect, label, ink, ButtonStyle.Tinted);
    }
}
