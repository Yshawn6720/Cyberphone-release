using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calculator;

internal static class CalculatorGlyphs
{
    public const float SizeFraction = 0.30f;
    private const float StrokeUnits = 2.6f;
    private const float ThinStrokeUnits = 2.0f;
    private const float TimesReach = 0.36f;
    private const float DivideDotOffset = 0.34f;
    private const float DivideDotRadius = 0.9f;
    private const float EqualsOffset = 0.16f;
    private const float PlusMinusArm = 0.20f;
    private const float PlusMinusShift = 0.30f;
    private const float PlusMinusSlash = 0.40f;
    private const float BackspaceWidth = 1.20f;
    private const float BackspaceHeight = 0.84f;
    private const float BackspaceCross = 0.17f;
    private const int DotSegments = 16;

    public static void Operator(ImDrawListPtr drawList, CalcOp op, Vector2 center, float size, Vector4 ink,
        float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = StrokeUnits * scale;
        var half = size * 0.5f;
        switch (op)
        {
            case CalcOp.Add:
                Horizontal(drawList, center, half, color, thickness);
                drawList.AddLine(center - new Vector2(0f, half), center + new Vector2(0f, half), color, thickness);
                return;
            case CalcOp.Subtract:
                Horizontal(drawList, center, half, color, thickness);
                return;
            case CalcOp.Multiply:
                var reach = size * TimesReach;
                drawList.AddLine(center - new Vector2(reach, reach), center + new Vector2(reach, reach), color,
                    thickness);
                drawList.AddLine(center + new Vector2(-reach, reach), center + new Vector2(reach, -reach), color,
                    thickness);
                return;
            case CalcOp.Divide:
                Horizontal(drawList, center, half, color, thickness);
                var dotOffset = new Vector2(0f, size * DivideDotOffset);
                var dotRadius = thickness * DivideDotRadius;
                drawList.AddCircleFilled(center - dotOffset, dotRadius, color, DotSegments);
                drawList.AddCircleFilled(center + dotOffset, dotRadius, color, DotSegments);
                return;
        }
    }

    public static void EqualsSign(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = StrokeUnits * scale;
        var offset = new Vector2(0f, size * EqualsOffset);
        Horizontal(drawList, center - offset, size * 0.5f, color, thickness);
        Horizontal(drawList, center + offset, size * 0.5f, color, thickness);
    }

    public static void PlusMinus(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = ThinStrokeUnits * scale;
        var arm = size * PlusMinusArm;
        var shift = size * PlusMinusShift;
        var plus = center + new Vector2(-shift, -shift);
        Horizontal(drawList, plus, arm, color, thickness);
        drawList.AddLine(plus - new Vector2(0f, arm), plus + new Vector2(0f, arm), color, thickness);
        Horizontal(drawList, center + new Vector2(shift, shift), arm, color, thickness);
        var slash = size * PlusMinusSlash;
        drawList.AddLine(center + new Vector2(slash * 0.5f, -slash), center + new Vector2(-slash * 0.5f, slash), color,
            thickness);
    }

    public static void Backspace(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = ThinStrokeUnits * scale;
        var halfWidth = size * BackspaceWidth * 0.5f;
        var halfHeight = size * BackspaceHeight * 0.5f;
        var left = center.X - halfWidth;
        var right = center.X + halfWidth;
        var shoulder = left + halfHeight;
        drawList.PathLineTo(new Vector2(left, center.Y));
        drawList.PathLineTo(new Vector2(shoulder, center.Y - halfHeight));
        drawList.PathLineTo(new Vector2(right, center.Y - halfHeight));
        drawList.PathLineTo(new Vector2(right, center.Y + halfHeight));
        drawList.PathLineTo(new Vector2(shoulder, center.Y + halfHeight));
        drawList.PathStroke(color, ImDrawFlags.Closed, thickness);
        var crossCenter = new Vector2((shoulder + right) * 0.5f, center.Y);
        var cross = size * BackspaceCross;
        drawList.AddLine(crossCenter - new Vector2(cross, cross), crossCenter + new Vector2(cross, cross), color,
            thickness);
        drawList.AddLine(crossCenter + new Vector2(-cross, cross), crossCenter + new Vector2(cross, -cross), color,
            thickness);
    }

    private static void Horizontal(ImDrawListPtr drawList, Vector2 center, float half, uint color, float thickness) =>
        drawList.AddLine(center - new Vector2(half, 0f), center + new Vector2(half, 0f), color, thickness);
}
