using Aetherphone.Core.Casino;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class BarkeepArt
{
    public static readonly Vector4 PerfectGold = new(1f, 0.84f, 0.42f, 1f);

    public static readonly Vector4 RoughAmber = new(0.96f, 0.62f, 0.30f, 1f);

    public static readonly Vector4 MissRed = new(0.94f, 0.38f, 0.38f, 1f);

    public static Vector4 GradeTint(int grade, Vector4 accent)
    {
        return grade switch
        {
            BarkeepGrading.PerfectGrade => PerfectGold,
            BarkeepGrading.GoodGrade => accent,
            BarkeepGrading.RoughGrade => RoughAmber,
            _ => MissRed,
        };
    }

    public static void DrawVerbGlyph(ImDrawListPtr drawList, int stepKind, Vector2 center, float extent, uint ink)
    {
        switch (stepKind)
        {
            case BarkeepRules.PourKind:
                DrawGlass(drawList, center, extent, ink);
                break;
            case BarkeepRules.ShakeKind:
                DrawShaker(drawList, center, extent, ink);
                break;
            case BarkeepRules.LayerKind:
                DrawLayers(drawList, center, extent, ink);
                break;
            default:
                DrawCherry(drawList, center, extent, ink);
                break;
        }
    }

    private static void DrawGlass(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var thickness = MathF.Max(1f, extent * 0.18f);
        var topY = center.Y - extent * 0.75f;
        var bottomY = center.Y + extent * 0.75f;
        drawList.AddLine(new Vector2(center.X - extent * 0.62f, topY),
            new Vector2(center.X - extent * 0.36f, bottomY), ink, thickness);
        drawList.AddLine(new Vector2(center.X + extent * 0.62f, topY),
            new Vector2(center.X + extent * 0.36f, bottomY), ink, thickness);
        drawList.AddLine(new Vector2(center.X - extent * 0.36f, bottomY),
            new Vector2(center.X + extent * 0.36f, bottomY), ink, thickness);
        drawList.AddLine(new Vector2(center.X - extent * 0.5f, center.Y),
            new Vector2(center.X + extent * 0.5f, center.Y), ink, thickness * 0.8f);
    }

    private static void DrawShaker(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        drawList.AddRectFilled(new Vector2(center.X - extent * 0.4f, center.Y - extent * 0.25f),
            new Vector2(center.X + extent * 0.4f, center.Y + extent * 0.8f), ink, extent * 0.16f);
        drawList.AddRectFilled(new Vector2(center.X - extent * 0.3f, center.Y - extent * 0.6f),
            new Vector2(center.X + extent * 0.3f, center.Y - extent * 0.3f), ink, extent * 0.1f);
        drawList.AddRectFilled(new Vector2(center.X - extent * 0.14f, center.Y - extent * 0.85f),
            new Vector2(center.X + extent * 0.14f, center.Y - extent * 0.6f), ink, extent * 0.08f);
    }

    private static void DrawLayers(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var thickness = MathF.Max(1f, extent * 0.22f);
        for (var layerIndex = 0; layerIndex < 3; layerIndex++)
        {
            var y = center.Y - extent * 0.5f + layerIndex * extent * 0.5f;
            var halfWidth = extent * (0.7f - layerIndex * 0.12f);
            drawList.AddLine(new Vector2(center.X - halfWidth, y), new Vector2(center.X + halfWidth, y), ink,
                thickness);
        }
    }

    private static void DrawCherry(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        drawList.AddCircleFilled(new Vector2(center.X - extent * 0.15f, center.Y + extent * 0.35f),
            extent * 0.38f, ink, 20);
        drawList.PathClear();
        drawList.PathLineTo(new Vector2(center.X - extent * 0.15f, center.Y + extent * 0.05f));
        drawList.PathBezierCubicCurveTo(new Vector2(center.X, center.Y - extent * 0.5f),
            new Vector2(center.X + extent * 0.35f, center.Y - extent * 0.7f),
            new Vector2(center.X + extent * 0.55f, center.Y - extent * 0.75f), 12);
        drawList.PathStroke(ink, ImDrawFlags.None, MathF.Max(1f, extent * 0.14f));
    }
}
