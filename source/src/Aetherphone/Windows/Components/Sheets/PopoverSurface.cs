using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PopoverSurface
{
    public static void Draw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, PhoneTheme theme,
        float scale, float alpha = 1f) =>
        Draw(drawList, min, max, rounding, SheetSkin.From(theme), scale, alpha);

    public static void DrawGlass(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, SocialInk ink,
        float scale, float alpha = 1f) =>
        Draw(drawList, min, max, rounding, SheetSkin.From(ink), scale, alpha);

    public static void Draw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, in SheetSkin skin,
        float scale, float alpha = 1f)
    {
        Elevation.Floating(drawList, min, max, rounding, scale, alpha);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(skin.Panel, skin.Panel.W * alpha)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(skin.Stroke, skin.Stroke.W * alpha)), Metrics.Stroke.Hairline);
    }
}
