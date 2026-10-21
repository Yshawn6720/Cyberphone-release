using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PhoneIcon
{
    public static void Draw(ImDrawListPtr drawList, Vector2 center, string glyph, Vector4 color, float boxHeight) =>
        Draw(drawList, center, glyph, ImGui.GetColorU32(color), boxHeight);

    public static unsafe void Draw(ImDrawListPtr drawList, Vector2 center, string glyph, uint color,
        float boxHeight)
    {
        using (Plugin.Fonts.PushIcon(boxHeight, glyph))
        {
            var font = ImGui.GetFont();
            if (font.FontSize <= 0f)
            {
                return;
            }

            ImFontGlyphPtr found = font.FindGlyph(glyph[0]);
            if (found.IsNull)
            {
                return;
            }

            var ratio = boxHeight / font.FontSize;
            var pen = new Vector2(center.X - (found.X0 + found.X1) * 0.5f * ratio,
                center.Y - (found.Y0 + found.Y1) * 0.5f * ratio);
            drawList.AddText(font, boxHeight, pen, color, glyph);
        }
    }
}
