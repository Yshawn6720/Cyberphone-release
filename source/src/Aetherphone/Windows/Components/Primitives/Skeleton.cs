using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Skeleton
{
    private const float PulsePeriodMs = 1600f;
    private const float BaseAlpha = 0.05f;
    private const float PulseAlpha = 0.04f;

    public static uint Fill(float alpha = 1f) =>
        ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (BaseAlpha + PulseAlpha * Pulse.Wave(PulsePeriodMs)) * alpha));

    public static void Bar(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float alpha = 1f)
    {
        Squircle.Fill(drawList, min, max, rounding, Fill(alpha));
    }

    public static void Disc(ImDrawListPtr drawList, Vector2 center, float radius, float alpha = 1f)
    {
        drawList.AddCircleFilled(center, radius, Fill(alpha), 32);
    }

    public static void Row(ImDrawListPtr drawList, Rect row, float scale, float alpha = 1f)
    {
        var radius = MathF.Min(row.Height * 0.34f, 17f * scale);
        var center = new Vector2(row.Min.X + radius, row.Center.Y);
        Disc(drawList, center, radius, alpha);
        var textLeft = center.X + radius + 11f * scale;
        var lineHeight = 9f * scale;
        Bar(drawList, new Vector2(textLeft, row.Center.Y - lineHeight - 3f * scale),
            new Vector2(textLeft + (row.Max.X - textLeft) * 0.52f, row.Center.Y - 3f * scale),
            lineHeight * 0.5f, alpha);
        Bar(drawList, new Vector2(textLeft, row.Center.Y + 3f * scale),
            new Vector2(textLeft + (row.Max.X - textLeft) * 0.78f, row.Center.Y + 3f * scale + lineHeight),
            lineHeight * 0.5f, alpha);
    }

    public static void Rows(ImDrawListPtr drawList, Rect area, float rowHeightUnits, float gapUnits, float scale,
        float alpha = 1f)
    {
        var rowHeight = rowHeightUnits * scale;
        var gap = gapUnits * scale;
        var y = area.Min.Y;
        while (y + rowHeight <= area.Max.Y)
        {
            Row(drawList, new Rect(new Vector2(area.Min.X, y), new Vector2(area.Max.X, y + rowHeight)), scale, alpha);
            y += rowHeight + gap;
        }
    }

    public static float PostCard(ImDrawListPtr drawList, Rect area, float y, float scale, float alpha = 1f)
    {
        var avatarRadius = 16f * scale;
        var avatarCenter = new Vector2(area.Min.X + avatarRadius, y + avatarRadius + 4f * scale);
        Disc(drawList, avatarCenter, avatarRadius, alpha);
        var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var lineHeight = 9f * scale;
        Bar(drawList, new Vector2(textLeft, avatarCenter.Y - lineHeight - 2f * scale),
            new Vector2(textLeft + (area.Max.X - textLeft) * 0.42f, avatarCenter.Y - 2f * scale),
            lineHeight * 0.5f, alpha);
        Bar(drawList, new Vector2(textLeft, avatarCenter.Y + 2f * scale),
            new Vector2(textLeft + (area.Max.X - textLeft) * 0.26f, avatarCenter.Y + 2f * scale + lineHeight),
            lineHeight * 0.5f, alpha);
        var mediaTop = avatarCenter.Y + avatarRadius + 10f * scale;
        var mediaBottom = mediaTop + 150f * scale;
        Bar(drawList, new Vector2(area.Min.X, mediaTop), new Vector2(area.Max.X, mediaBottom), 14f * scale, alpha);
        return mediaBottom + 18f * scale;
    }

    public static void Feed(ImDrawListPtr drawList, Rect area, float scale, float alpha = 1f)
    {
        var y = area.Min.Y;
        while (y < area.Max.Y)
        {
            y = PostCard(drawList, area, y, scale, alpha);
        }
    }
}
