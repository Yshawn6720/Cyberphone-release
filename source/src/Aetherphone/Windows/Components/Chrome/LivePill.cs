using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class LivePill
{
    private const float DotRadius = 3.2f;
    private const float PaddingX = 7f;
    private const float PaddingY = 3f;
    private const float DotGap = 5f;
    private const float PulsePeriodSeconds = 1.8f;
    private const float PulseFloor = 0.5f;
    private const float FillAlpha = 0.16f;
    private const float Rounding = 0.34f;

    public static float Height(float scale)
    {
        return Typography.LineHeight(TextStyles.Caption2) + PaddingY * 2f * scale;
    }

    public static float Width(string label, float scale)
    {
        return PaddingX * 2f * scale + DotRadius * 2f * scale + DotGap * scale
            + Typography.Measure(label, TextStyles.Caption2).X;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 origin, string label, Vector4 accent, float clock,
        float scale)
    {
        var height = Height(scale);
        var max = origin + new Vector2(Width(label, scale), height);
        Squircle.Fill(drawList, origin, max, height * Rounding,
            ImGui.GetColorU32(Palette.WithAlpha(accent, FillAlpha)));
        DrawLamp(drawList, new Vector2(origin.X + PaddingX * scale + DotRadius * scale, (origin.Y + max.Y) * 0.5f),
            accent, clock, scale);
        var size = Typography.Measure(label, TextStyles.Caption2);
        Typography.Draw(drawList,
            new Vector2(origin.X + PaddingX * scale + DotRadius * 2f * scale + DotGap * scale,
                (origin.Y + max.Y) * 0.5f - size.Y * 0.5f), label, accent, TextStyles.Caption2);
    }

    public static void DrawLamp(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float clock, float scale)
    {
        var pulse = PulseFloor + (1f - PulseFloor) * MathF.Abs(MathF.Sin(clock * MathF.PI / PulsePeriodSeconds));
        drawList.AddCircleFilled(center, DotRadius * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, pulse)), 16);
    }
}
