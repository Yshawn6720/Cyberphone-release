using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class SlotsSymbolArt
{
    public static readonly Vector4[] SymbolTints =
    {
        new(1.00f, 0.80f, 0.28f, 1f),
        new(0.38f, 0.82f, 0.96f, 1f),
        new(0.98f, 0.62f, 0.30f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
        new(0.64f, 0.72f, 0.86f, 1f),
        new(0.94f, 0.50f, 0.62f, 1f),
        new(0.46f, 0.84f, 0.62f, 1f),
        new(0.55f, 0.64f, 0.94f, 1f),
        new(1.00f, 0.88f, 0.45f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
    };

    public static void Draw(ImDrawListPtr drawList, int symbolId, Vector2 center, float extent, float alpha = 1f)
    {
        var tintIndex = symbolId >= 0 && symbolId < SymbolTints.Length ? symbolId : 0;
        var tint = SymbolTints[tintIndex] with { W = alpha };
        var ink = ImGui.GetColorU32(tint);
        var shade = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f * alpha));
        var glint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.75f * alpha));
        switch (symbolId)
        {
            case 0:
                DrawCrown(drawList, center, extent, ink, shade);
                break;
            case 1:
                DrawGem(drawList, center, extent, ink, glint);
                break;
            case 2:
                DrawBell(drawList, center, extent, ink, shade);
                break;
            case 3:
                DrawSeven(drawList, center, extent, ink);
                break;
            case 4:
                DrawSpade(drawList, center, extent, ink);
                break;
            case 5:
                DrawHeart(drawList, center, extent, ink);
                break;
            case 6:
                DrawDiamond(drawList, center, extent, ink, glint);
                break;
            case 7:
                DrawClub(drawList, center, extent, ink);
                break;
            case 8:
                DrawWild(drawList, center, extent, ink, shade, glint);
                break;
            default:
                DrawDisc(drawList, center, extent, ink, glint);
                break;
        }
    }

    private static void DrawCrown(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint shade)
    {
        var baseTop = center.Y + extent * 0.35f;
        var baseBottom = center.Y + extent * 0.75f;
        drawList.AddRectFilled(new Vector2(center.X - extent * 0.85f, baseTop),
            new Vector2(center.X + extent * 0.85f, baseBottom), ink, extent * 0.12f);
        Span<float> tipOffsets = stackalloc float[3] { -0.72f, 0f, 0.72f };
        Span<Vector2> spike = stackalloc Vector2[3];
        for (var tipIndex = 0; tipIndex < tipOffsets.Length; tipIndex++)
        {
            var tipX = center.X + tipOffsets[tipIndex] * extent;
            var tall = tipIndex == 1 ? 1.0f : 0.72f;
            spike[0] = new Vector2(tipX - extent * 0.32f, baseTop + extent * 0.05f);
            spike[1] = new Vector2(tipX + extent * 0.32f, baseTop + extent * 0.05f);
            spike[2] = new Vector2(tipX, center.Y - extent * tall);
            FillConvex(drawList, ink, spike);
            drawList.AddCircleFilled(new Vector2(tipX, center.Y - extent * tall), extent * 0.14f, ink, 12);
        }

        drawList.AddCircleFilled(new Vector2(center.X, (baseTop + baseBottom) * 0.5f), extent * 0.12f, shade, 12);
    }

    private static void DrawGem(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint glint)
    {
        Span<Vector2> hexagon = stackalloc Vector2[6]
        {
            new(center.X - extent * 0.55f, center.Y - extent * 0.62f),
            new(center.X + extent * 0.55f, center.Y - extent * 0.62f),
            new(center.X + extent * 0.92f, center.Y - extent * 0.12f),
            new(center.X, center.Y + extent * 0.88f),
            new(center.X - extent * 0.92f, center.Y - extent * 0.12f),
            new(center.X - extent * 0.55f, center.Y - extent * 0.62f),
        };
        FillConvex(drawList, ink, hexagon[..5]);
        var thickness = MathF.Max(1f, extent * 0.09f);
        drawList.AddLine(hexagon[4], new Vector2(center.X, center.Y - extent * 0.12f), glint, thickness);
        drawList.AddLine(hexagon[2], new Vector2(center.X, center.Y - extent * 0.12f), glint, thickness);
        drawList.AddLine(new Vector2(center.X, center.Y - extent * 0.12f), hexagon[3], glint, thickness);
    }

    private static void DrawBell(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint shade)
    {
        var mouthY = center.Y + extent * 0.42f;
        drawList.PathClear();
        drawList.PathLineTo(new Vector2(center.X - extent * 0.85f, mouthY));
        drawList.PathArcTo(new Vector2(center.X, center.Y - extent * 0.1f), extent * 0.62f, MathF.PI, MathF.PI * 2f, 20);
        drawList.PathLineTo(new Vector2(center.X + extent * 0.85f, mouthY));
        drawList.PathFillConvex(ink);
        drawList.AddCircleFilled(new Vector2(center.X, center.Y - extent * 0.78f), extent * 0.16f, ink, 12);
        drawList.AddCircleFilled(new Vector2(center.X, mouthY + extent * 0.2f), extent * 0.18f, ink, 12);
        drawList.AddLine(new Vector2(center.X - extent * 0.85f, mouthY),
            new Vector2(center.X + extent * 0.85f, mouthY), shade, MathF.Max(1f, extent * 0.1f));
    }

    private static void DrawSeven(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var thickness = extent * 0.34f;
        drawList.AddLine(new Vector2(center.X - extent * 0.66f, center.Y - extent * 0.62f),
            new Vector2(center.X + extent * 0.66f, center.Y - extent * 0.62f), ink, thickness);
        drawList.AddLine(new Vector2(center.X + extent * 0.6f, center.Y - extent * 0.62f),
            new Vector2(center.X - extent * 0.12f, center.Y + extent * 0.82f), ink, thickness);
    }

    private static void DrawSpade(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var lobe = extent * 0.42f;
        drawList.AddCircleFilled(new Vector2(center.X - lobe, center.Y + extent * 0.14f), lobe, ink, 20);
        drawList.AddCircleFilled(new Vector2(center.X + lobe, center.Y + extent * 0.14f), lobe, ink, 20);
        Span<Vector2> tip = stackalloc Vector2[3]
        {
            new(center.X - extent * 0.8f, center.Y + extent * 0.05f),
            new(center.X + extent * 0.8f, center.Y + extent * 0.05f),
            new(center.X, center.Y - extent * 0.85f),
        };
        FillConvex(drawList, ink, tip);
        Span<Vector2> stem = stackalloc Vector2[4]
        {
            new(center.X - extent * 0.1f, center.Y + extent * 0.2f),
            new(center.X + extent * 0.1f, center.Y + extent * 0.2f),
            new(center.X + extent * 0.24f, center.Y + extent * 0.88f),
            new(center.X - extent * 0.24f, center.Y + extent * 0.88f),
        };
        FillConvex(drawList, ink, stem);
    }

    private static void DrawHeart(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var lobe = extent * 0.45f;
        drawList.AddCircleFilled(new Vector2(center.X - lobe, center.Y - extent * 0.3f), lobe, ink, 20);
        drawList.AddCircleFilled(new Vector2(center.X + lobe, center.Y - extent * 0.3f), lobe, ink, 20);
        Span<Vector2> tip = stackalloc Vector2[3]
        {
            new(center.X - extent * 0.86f, center.Y - extent * 0.14f),
            new(center.X + extent * 0.86f, center.Y - extent * 0.14f),
            new(center.X, center.Y + extent * 0.85f),
        };
        FillConvex(drawList, ink, tip);
    }

    private static void DrawDiamond(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint glint)
    {
        Span<Vector2> diamond = stackalloc Vector2[4]
        {
            new(center.X, center.Y - extent * 0.9f),
            new(center.X + extent * 0.68f, center.Y),
            new(center.X, center.Y + extent * 0.9f),
            new(center.X - extent * 0.68f, center.Y),
        };
        FillConvex(drawList, ink, diamond);
        drawList.AddLine(new Vector2(center.X - extent * 0.2f, center.Y - extent * 0.32f),
            new Vector2(center.X + extent * 0.08f, center.Y - extent * 0.55f), glint,
            MathF.Max(1f, extent * 0.1f));
    }

    private static void DrawClub(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var lobe = extent * 0.38f;
        drawList.AddCircleFilled(new Vector2(center.X, center.Y - extent * 0.45f), lobe, ink, 20);
        drawList.AddCircleFilled(new Vector2(center.X - extent * 0.42f, center.Y + extent * 0.1f), lobe, ink, 20);
        drawList.AddCircleFilled(new Vector2(center.X + extent * 0.42f, center.Y + extent * 0.1f), lobe, ink, 20);
        Span<Vector2> stem = stackalloc Vector2[4]
        {
            new(center.X - extent * 0.1f, center.Y + extent * 0.1f),
            new(center.X + extent * 0.1f, center.Y + extent * 0.1f),
            new(center.X + extent * 0.24f, center.Y + extent * 0.88f),
            new(center.X - extent * 0.24f, center.Y + extent * 0.88f),
        };
        FillConvex(drawList, ink, stem);
    }

    private static void DrawWild(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint shade,
        uint glint)
    {
        var half = extent * 0.88f;
        drawList.AddRectFilled(new Vector2(center.X - half, center.Y - half),
            new Vector2(center.X + half, center.Y + half), ink, extent * 0.3f);
        drawList.AddRectFilled(new Vector2(center.X - half, center.Y),
            new Vector2(center.X + half, center.Y + half), shade, extent * 0.3f,
            ImDrawFlags.RoundCornersBottom);
        DrawSparkle(drawList, center, extent * 0.62f, glint);
    }

    private static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint glint)
    {
        drawList.PathClear();
        drawList.PathArcTo(new Vector2(center.X, center.Y + extent * 0.05f), extent * 0.45f, MathF.PI,
            MathF.PI * 2f, 16);
        drawList.PathFillConvex(glint);
        drawList.AddRectFilled(new Vector2(center.X - extent * 0.95f, center.Y - extent * 0.02f),
            new Vector2(center.X + extent * 0.95f, center.Y + extent * 0.4f), ink, extent * 0.24f);
        Span<float> lightOffsets = stackalloc float[3] { -0.55f, 0f, 0.55f };
        for (var lightIndex = 0; lightIndex < lightOffsets.Length; lightIndex++)
        {
            drawList.AddCircleFilled(
                new Vector2(center.X + lightOffsets[lightIndex] * extent, center.Y + extent * 0.19f),
                extent * 0.1f, glint, 10);
        }

        drawList.AddLine(new Vector2(center.X - extent * 0.4f, center.Y + extent * 0.62f),
            new Vector2(center.X + extent * 0.4f, center.Y + extent * 0.62f), ink,
            MathF.Max(1f, extent * 0.12f));
    }

    public static void DrawSparkle(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        Span<Vector2> inner = stackalloc Vector2[4]
        {
            new(center.X + extent * 0.22f, center.Y - extent * 0.22f),
            new(center.X + extent * 0.22f, center.Y + extent * 0.22f),
            new(center.X - extent * 0.22f, center.Y + extent * 0.22f),
            new(center.X - extent * 0.22f, center.Y - extent * 0.22f),
        };
        Span<Vector2> tips = stackalloc Vector2[4]
        {
            new(center.X, center.Y - extent),
            new(center.X + extent, center.Y),
            new(center.X, center.Y + extent),
            new(center.X - extent, center.Y),
        };
        Span<Vector2> kite = stackalloc Vector2[3];
        for (var arm = 0; arm < 4; arm++)
        {
            kite[0] = tips[arm];
            kite[1] = inner[arm];
            kite[2] = inner[(arm + 3) % 4];
            FillConvex(drawList, ink, kite);
        }

        FillConvex(drawList, ink, inner);
    }

    private static void FillConvex(ImDrawListPtr drawList, uint color, ReadOnlySpan<Vector2> points)
    {
        drawList.PathClear();
        for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
        {
            drawList.PathLineTo(points[pointIndex]);
        }

        drawList.PathFillConvex(color);
    }
}
