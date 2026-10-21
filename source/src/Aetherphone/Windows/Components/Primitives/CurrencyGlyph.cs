using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum CurrencyKind
{
    Chips,
    Coins,
}

internal static class CurrencyGlyph
{
    public const float GlyphFraction = 0.72f;
    public const float GapFraction = 0.30f;

    private const int NotchCount = 4;
    private const int CircleSegments = 24;

    private static readonly Vector4 ChipBody = new(0.86f, 0.30f, 0.40f, 1f);
    private static readonly Vector4 ChipNotch = new(0.97f, 0.95f, 0.93f, 1f);
    private static readonly Vector4 ChipRim = new(0f, 0f, 0f, 0.32f);
    private static readonly Vector4 CoinBody = new(0.98f, 0.80f, 0.36f, 1f);
    private static readonly Vector4 CoinEdge = new(0.66f, 0.47f, 0.14f, 1f);

    public static float Reserve(float lineHeight) => lineHeight * (GlyphFraction + GapFraction);

    public static void Draw(ImDrawListPtr drawList, CurrencyKind kind, Vector2 center, float size, float alpha = 1f)
    {
        if (kind == CurrencyKind.Chips)
        {
            DrawChip(drawList, center, size * 0.5f, alpha);
            return;
        }

        DrawCoin(drawList, center, size * 0.5f, alpha);
    }

    public static Vector2 MeasureAmount(string amountText, in TextStyle style)
    {
        var textSize = Typography.Measure(amountText, style);
        return new Vector2(Reserve(textSize.Y) + textSize.X, textSize.Y);
    }

    public static Vector2 DrawAmount(ImDrawListPtr drawList, Vector2 position, string amountText, CurrencyKind kind,
        Vector4 ink, in TextStyle style, float alpha = 1f)
    {
        var textSize = Typography.Measure(amountText, style);
        var glyphSize = textSize.Y * GlyphFraction;
        Draw(drawList, kind, new Vector2(position.X + glyphSize * 0.5f, position.Y + textSize.Y * 0.5f), glyphSize,
            alpha);
        Typography.Draw(drawList, new Vector2(position.X + Reserve(textSize.Y), position.Y), amountText, ink, style);
        return new Vector2(Reserve(textSize.Y) + textSize.X, textSize.Y);
    }

    private static void DrawChip(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ChipBody, alpha)),
            CircleSegments);
        var notchColor = ImGui.GetColorU32(Palette.WithAlpha(ChipNotch, 0.92f * alpha));
        for (var notch = 0; notch < NotchCount; notch++)
        {
            var angle = MathF.PI * 0.25f + notch * (MathF.PI * 0.5f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(center + direction * radius * 0.58f, center + direction * radius * 0.94f, notchColor,
                radius * 0.30f);
        }

        drawList.AddCircle(center, radius * 0.56f, notchColor, CircleSegments, radius * 0.13f);
        drawList.AddCircle(center, radius * 0.97f, ImGui.GetColorU32(Palette.WithAlpha(ChipRim, alpha)),
            CircleSegments, radius * 0.10f);
    }

    private static void DrawCoin(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(CoinBody, alpha)),
            CircleSegments);
        var edgeColor = ImGui.GetColorU32(Palette.WithAlpha(CoinEdge, alpha));
        drawList.AddCircle(center, radius * 0.94f, edgeColor, CircleSegments, radius * 0.14f);
        var reach = radius * 0.52f;
        var waist = radius * 0.16f;
        drawList.AddQuadFilled(center + new Vector2(0f, -reach), center + new Vector2(waist, 0f),
            center + new Vector2(0f, reach), center + new Vector2(-waist, 0f), edgeColor);
        drawList.AddQuadFilled(center + new Vector2(-reach, 0f), center + new Vector2(0f, -waist),
            center + new Vector2(reach, 0f), center + new Vector2(0f, waist), edgeColor);
    }
}
