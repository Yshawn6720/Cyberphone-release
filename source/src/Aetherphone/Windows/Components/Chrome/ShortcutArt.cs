using Aetherphone.Core;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static class ShortcutArt
{
    private const float RadiusFactor = 0.26f;

    public static void DrawSurface(ImDrawListPtr drawList, Vector2 center, float size, ShortcutEntry shortcut,
        IDalamudTextureWrap? icon, float scale)
    {
        var half = size * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var radius = size * RadiusFactor;
        var surface = IconTile.Surface(ShortcutTint.Resolve(shortcut.Tint));
        Elevation.IconRest(drawList, min, max, radius, scale);
        IconTile.FillShaded(drawList, min, max, radius, surface);
        DrawContent(drawList, center, size, shortcut, icon, AccentRing.Ink);
        Material.EdgeSquircle(drawList, min, max, radius, scale);
    }

    private static void DrawContent(ImDrawListPtr drawList, Vector2 center, float size, ShortcutEntry shortcut,
        IDalamudTextureWrap? icon, Vector4 glyphInk)
    {
        if (icon is not null)
        {
            var half = size * 0.5f;
            Squircle.FillImage(drawList, new Vector2(center.X - half, center.Y - half),
                new Vector2(center.X + half, center.Y + half), size * RadiusFactor, icon.Handle, 0xFFFFFFFFu);
            return;
        }

        if (shortcut.Glyph != 0)
        {
            ProgressRing.CenterIcon(drawList, center, (FontAwesomeIcon)shortcut.Glyph, glyphInk, size * 0.42f);
            return;
        }

        var monogram = ShortcutStore.Monogram(shortcut.Name);
        var measured = Typography.Measure(monogram, TextStyles.Title1);
        var glyphScale = measured.Y > 0f ? size * 0.44f / measured.Y : 1f;
        Typography.DrawCentered(drawList, center, monogram, glyphInk, TextStyles.Title1.Scale * glyphScale,
            FontWeight.SemiBold);
    }
}
