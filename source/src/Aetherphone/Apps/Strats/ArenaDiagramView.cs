using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Strats;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Strats;

internal static class ArenaDiagramView
{
    private static readonly Vector4 Floor = new(0.16f, 0.17f, 0.24f, 1f);
    private static readonly Vector4 FloorLine = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 Aoe = new(0.98f, 0.55f, 0.15f, 1f);
    private static readonly Vector4 Tank = new(0.23f, 0.51f, 0.96f, 1f);
    private static readonly Vector4 Healer = new(0.13f, 0.77f, 0.37f, 1f);
    private static readonly Vector4 Damage = new(0.94f, 0.27f, 0.27f, 1f);
    private static readonly Vector4 Support = new(0.08f, 0.72f, 0.65f, 1f);
    private static readonly Vector4 MarkRed = new(0.94f, 0.27f, 0.27f, 1f);
    private static readonly Vector4 MarkYellow = new(0.92f, 0.70f, 0.03f, 1f);
    private static readonly Vector4 MarkBlue = new(0.23f, 0.51f, 0.96f, 1f);
    private static readonly Vector4 MarkPurple = new(0.66f, 0.33f, 0.97f, 1f);
    private static readonly Vector4 Boss = new(0.92f, 0.30f, 0.30f, 1f);

    public static void Draw(ImDrawListPtr drawList, Rect stage, ArenaDiagram arena, float scale, Vector4 ink)
    {
        var unit = stage.Width / 100f;
        drawList.PushClipRect(stage.Min, stage.Max, true);
        var floor = ParseColor(arena.Background, Floor);
        if (arena.Shape == "circle")
        {
            drawList.AddCircleFilled(stage.Center, stage.Width * 0.5f, ImGui.GetColorU32(floor), 64);
            drawList.AddCircle(stage.Center, stage.Width * 0.5f, ImGui.GetColorU32(FloorLine), 64, scale);
        }
        else
        {
            drawList.AddRectFilled(stage.Min, stage.Max, ImGui.GetColorU32(floor), Metrics.Radius.Sm * scale);
        }

        for (var index = 0; index < arena.Elements.Length; index++)
        {
            var element = arena.Elements[index];
            switch (element.Type)
            {
                case "shape":
                    DrawShape(drawList, stage, unit, element, scale);
                    break;
                case "aoeCircle":
                    DrawAoeCircle(drawList, stage, unit, element);
                    break;
                case "aoeRect":
                    DrawAoeRect(drawList, stage, unit, element);
                    break;
                case "tether":
                case "arrow":
                    DrawLine(drawList, stage, unit, element, scale, element.Type == "arrow");
                    break;
                case "waymark":
                    DrawWaymark(drawList, stage, unit, element, scale);
                    break;
                case "boss":
                    DrawBoss(drawList, stage, unit, element, scale);
                    break;
                case "player":
                    DrawPlayer(drawList, stage, unit, element, scale);
                    break;
                case "text":
                    DrawText(drawList, stage, unit, element, ink);
                    break;
            }
        }

        drawList.PopClipRect();
    }

    private static Vector2 Point(Rect stage, float unit, float x, float y) =>
        new(stage.Min.X + x * unit, stage.Min.Y + y * unit);

    private static void DrawShape(ImDrawListPtr drawList, Rect stage, float unit, ArenaElement element, float scale)
    {
        var center = Point(stage, unit, element.X, element.Y);
        var fill = ImGui.GetColorU32(ParseColor(element.Color, Floor));
        var line = ImGui.GetColorU32(FloorLine);
        if (element.Shape == "circle")
        {
            var radius = element.Width * 0.5f * unit;
            drawList.AddCircleFilled(center, radius, fill, 64);
            drawList.AddCircle(center, radius, line, 64, scale);
            drawList.AddLine(new Vector2(center.X - radius, center.Y), new Vector2(center.X + radius, center.Y), line, scale);
            drawList.AddLine(new Vector2(center.X, center.Y - radius), new Vector2(center.X, center.Y + radius), line, scale);
            return;
        }

        var half = new Vector2(element.Width * 0.5f * unit, element.Height * 0.5f * unit);
        drawList.AddRectFilled(center - half, center + half, fill);
        drawList.AddRect(center - half, center + half, line, 0f, ImDrawFlags.None, scale);
        drawList.AddLine(new Vector2(center.X - half.X, center.Y), new Vector2(center.X + half.X, center.Y), line, scale);
        drawList.AddLine(new Vector2(center.X, center.Y - half.Y), new Vector2(center.X, center.Y + half.Y), line, scale);
    }

    private static void DrawAoeCircle(ImDrawListPtr drawList, Rect stage, float unit, ArenaElement element)
    {
        var color = ParseColor(element.Color, Aoe) with { W = Math.Clamp(element.Opacity, 0.05f, 1f) * 0.55f };
        var center = Point(stage, unit, element.X, element.Y);
        var radiusX = element.Radius * unit;
        var radiusY = (element.Height > 0f ? element.Height : element.Radius) * unit;
        if (MathF.Abs(radiusX - radiusY) < 0.5f)
        {
            drawList.AddCircleFilled(center, radiusX, ImGui.GetColorU32(color), 48);
            return;
        }

        var rotation = element.Rotation * MathF.PI / 180f;
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        drawList.PathClear();
        for (var index = 0; index < 48; index++)
        {
            var angle = index * MathF.PI * 2f / 48f;
            var x = MathF.Cos(angle) * radiusX;
            var y = MathF.Sin(angle) * radiusY;
            drawList.PathLineTo(new Vector2(center.X + x * cos - y * sin, center.Y + x * sin + y * cos));
        }

        drawList.PathFillConvex(ImGui.GetColorU32(color));
    }

    private static void DrawAoeRect(ImDrawListPtr drawList, Rect stage, float unit, ArenaElement element)
    {
        var color = ParseColor(element.Color, Aoe) with { W = Math.Clamp(element.Opacity, 0.05f, 1f) * 0.55f };
        var center = Point(stage, unit, element.X, element.Y);
        var half = new Vector2(element.Width * 0.5f * unit, element.Height * 0.5f * unit);
        var rotation = element.Rotation * MathF.PI / 180f;
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        var a = Rotate(center, new Vector2(-half.X, -half.Y), cos, sin);
        var b = Rotate(center, new Vector2(half.X, -half.Y), cos, sin);
        var c = Rotate(center, new Vector2(half.X, half.Y), cos, sin);
        var d = Rotate(center, new Vector2(-half.X, half.Y), cos, sin);
        drawList.AddQuadFilled(a, b, c, d, ImGui.GetColorU32(color));
    }

    private static Vector2 Rotate(Vector2 center, Vector2 offset, float cos, float sin) =>
        new(center.X + offset.X * cos - offset.Y * sin, center.Y + offset.X * sin + offset.Y * cos);

    private static void DrawLine(ImDrawListPtr drawList, Rect stage, float unit, ArenaElement element, float scale,
        bool arrowHead)
    {
        var from = Point(stage, unit, element.X, element.Y);
        var to = Point(stage, unit, element.X2, element.Y2);
        var color = ImGui.GetColorU32(ParseColor(element.Color, Vector4.One));
        var thickness = MathF.Max(1f, (element.Width > 0f ? element.Width : 0.8f) * unit * 0.6f);
        drawList.AddLine(from, to, color, thickness);
        if (!arrowHead)
        {
            return;
        }

        var direction = to - from;
        var length = direction.Length();
        if (length < 0.01f)
        {
            return;
        }

        direction /= length;
        var normal = new Vector2(-direction.Y, direction.X);
        var headLength = 3.2f * unit;
        var headWidth = 1.8f * unit;
        var back = to - direction * headLength;
        drawList.AddTriangleFilled(to, back + normal * headWidth, back - normal * headWidth, color);
    }

    private static void DrawWaymark(ImDrawListPtr drawList, Rect stage, float unit, ArenaElement element, float scale)
    {
        var center = Point(stage, unit, element.X, element.Y);
        var size = (element.Size > 0f ? element.Size : 4f) * unit;
        var color = WaymarkColor(element.Mark);
        var isLetter = element.Mark.Length > 0 && char.IsLetter(element.Mark[0]);
        if (isLetter)
        {
            drawList.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(color with { W = 0.35f }), 24);
            drawList.AddCircle(center, size * 0.5f, ImGui.GetColorU32(color), 24, scale);
        }
        else
        {
            var half = new Vector2(size * 0.5f, size * 0.5f);
            drawList.AddRectFilled(center - half, center + half, ImGui.GetColorU32(color with { W = 0.35f }));
            drawList.AddRect(center - half, center + half, ImGui.GetColorU32(color), 0f, ImDrawFlags.None, scale);
        }

        Typography.DrawCentered(drawList, center, element.Mark, color, new TextStyle(0.62f, FontWeight.Bold));
    }

    private static void DrawBoss(ImDrawListPtr drawList, Rect stage, float unit, ArenaElement element, float scale)
    {
        var center = Point(stage, unit, element.X, element.Y);
        var radius = (element.Size > 0f ? element.Size : 12f) * 0.5f * unit;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Boss with { W = 0.28f }), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Boss), 40, 1.5f * scale);
        var facing = element.Rotation * MathF.PI / 180f;
        var tip = new Vector2(center.X + MathF.Sin(facing) * radius, center.Y - MathF.Cos(facing) * radius);
        drawList.AddCircleFilled(tip, 1.6f * unit, ImGui.GetColorU32(Boss), 12);
    }

    private static void DrawPlayer(ImDrawListPtr drawList, Rect stage, float unit, ArenaElement element, float scale)
    {
        var center = Point(stage, unit, element.X, element.Y);
        var radius = (element.Size > 0f ? element.Size : 6f) * 0.5f * unit;
        var color = JobColor(element.Job);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 24);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Vector4.One with { W = 0.7f }), 24, scale);
        var label = element.Job.Length > 0 ? element.Job : "?";
        Typography.DrawCentered(drawList, center, label, Vector4.One, new TextStyle(0.58f, FontWeight.Bold));
        if (element.Marker.Length > 0)
        {
            var markerCenter = new Vector2(center.X, center.Y - radius - 1.6f * unit);
            Typography.DrawCentered(drawList, markerCenter, element.Marker, WaymarkColor(element.Marker),
                new TextStyle(0.55f, FontWeight.Bold));
        }
    }

    private static void DrawText(ImDrawListPtr drawList, Rect stage, float unit, ArenaElement element, Vector4 ink)
    {
        var position = Point(stage, unit, element.X, element.Y);
        var color = ParseColor(element.Color, ink);
        var textScale = Math.Clamp((element.Size > 0f ? element.Size : 4f) * 0.16f, 0.5f, 1.1f);
        Typography.DrawCentered(drawList, position, element.Text, color, new TextStyle(textScale, FontWeight.SemiBold));
    }

    private static Vector4 JobColor(string job)
    {
        if (job.StartsWith("MT", StringComparison.Ordinal) || job.StartsWith("OT", StringComparison.Ordinal) ||
            job == "TANK" || job == "T")
        {
            return Tank;
        }

        if (job.StartsWith('H'))
        {
            return Healer;
        }

        if (job == "SUP" || job == "G1" || job == "G2" || job == "TMR" || job == "HTM")
        {
            return Support;
        }

        return Damage;
    }

    private static Vector4 WaymarkColor(string mark) =>
        mark switch
        {
            "A" or "1" => MarkRed,
            "B" or "2" => MarkYellow,
            "C" or "3" => MarkBlue,
            "D" or "4" => MarkPurple,
            _ => Vector4.One,
        };

    private static Vector4 ParseColor(string hex, Vector4 fallback)
    {
        if (hex.Length != 7 || hex[0] != '#')
        {
            return fallback;
        }

        if (!int.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) ||
            !int.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) ||
            !int.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            return fallback;
        }

        return new Vector4(red / 255f, green / 255f, blue / 255f, 1f);
    }
}
