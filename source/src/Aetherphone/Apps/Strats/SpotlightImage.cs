using Aetherphone.Core;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Strats;

internal readonly struct ImageTransform
{
    private readonly float cos;
    private readonly float sin;
    private readonly float flipX;
    private readonly float flipY;
    public readonly bool SwapsAxes;
    public readonly bool IsIdentity;

    public ImageTransform(string kind)
    {
        var degrees = kind switch
        {
            "rotate90" => 90f,
            "rotate180" => 180f,
            "rotate270" => 270f,
            "rotate45" => 45f,
            "rotate315" => 315f,
            _ => 0f,
        };
        var radians = degrees * MathF.PI / 180f;
        cos = MathF.Cos(radians);
        sin = MathF.Sin(radians);
        flipX = kind == "flipX" ? -1f : 1f;
        flipY = kind == "flipY" ? -1f : 1f;
        SwapsAxes = kind == "rotate90" || kind == "rotate270";
        IsIdentity = kind.Length == 0;
    }

    public Vector2 Forward(Vector2 offset)
    {
        var x = offset.X * flipX;
        var y = offset.Y * flipY;
        return new Vector2(x * cos - y * sin, x * sin + y * cos);
    }

    public Vector2 Inverse(Vector2 offset)
    {
        var x = offset.X * cos + offset.Y * sin;
        var y = -offset.X * sin + offset.Y * cos;
        return new Vector2(x * flipX, y * flipY);
    }
}

internal static class SpotlightImage
{
    private const int CircleSegments = 48;
    private const float DimAlpha = 0.55f;
    private static readonly Vector2[] Corners = { Vector2.Zero, new(1f, 0f), Vector2.One, new(0f, 1f) };

    public static float HeightFor(ImageRef image, string transformKind, float width)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return width;
        }

        var transform = new ImageTransform(transformKind);
        var aspect = transform.SwapsAxes ? (float)image.Width / image.Height : (float)image.Height / image.Width;
        return width * aspect;
    }

    public static void Draw(ImDrawListPtr drawList, Rect frame, IDalamudTextureWrap? texture, SpotlightMask? mask,
        string transformKind, float rounding, float scale, Vector4 placeholder, Vector4 accent)
    {
        if (texture is null)
        {
            drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(placeholder), rounding);
            LoadingPulse.Spinner(frame.Center, 9f * scale, accent, 0.8f);
            return;
        }

        var transform = new ImageTransform(transformKind);
        DrawImage(drawList, frame, texture, transform, rounding, mask is not null);
        if (mask is not null)
        {
            DrawHoles(drawList, frame, texture, transform, mask, scale);
        }
    }

    public static void DrawOverlay(ImDrawListPtr drawList, Rect frame, IDalamudTextureWrap texture, SpotlightMask mask,
        float scale)
    {
        drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, DimAlpha)));
        DrawHoles(drawList, frame, texture, new ImageTransform(string.Empty), mask, scale);
    }

    private static void DrawImage(ImDrawListPtr drawList, Rect frame, IDalamudTextureWrap texture,
        in ImageTransform transform, float rounding, bool dimmed)
    {
        var tint = dimmed ? new Vector4(1f - DimAlpha, 1f - DimAlpha, 1f - DimAlpha, 1f) : Vector4.One;
        var color = ImGui.GetColorU32(tint);
        if (transform.IsIdentity)
        {
            drawList.AddImageRounded(texture.Handle, frame.Min, frame.Max, Vector2.Zero, Vector2.One, color, rounding);
            return;
        }

        var size = DrawnSize(frame, transform);
        drawList.PushClipRect(frame.Min, frame.Max, true);
        drawList.AddImageQuad(texture.Handle,
            Map(frame.Center, size, transform, Corners[0]), Map(frame.Center, size, transform, Corners[1]),
            Map(frame.Center, size, transform, Corners[2]), Map(frame.Center, size, transform, Corners[3]),
            Corners[0], Corners[1], Corners[2], Corners[3], color);
        drawList.PopClipRect();
    }

    private static void DrawHoles(ImDrawListPtr drawList, Rect frame, IDalamudTextureWrap texture,
        in ImageTransform transform, SpotlightMask mask, float scale)
    {
        var size = DrawnSize(frame, transform);
        var center = frame.Center;
        var ring = ImGui.GetColorU32(Vector4.One);
        drawList.PushClipRect(frame.Min, frame.Max, true);
        drawList.PushTextureID(texture.Handle);
        if (mask.Rect is { } rect)
        {
            var a = Map(center, size, transform, new Vector2(rect.X / 100f, rect.Y / 100f));
            var b = Map(center, size, transform, new Vector2((rect.X + rect.Width) / 100f, rect.Y / 100f));
            var c = Map(center, size, transform,
                new Vector2((rect.X + rect.Width) / 100f, (rect.Y + rect.Height) / 100f));
            var d = Map(center, size, transform, new Vector2(rect.X / 100f, (rect.Y + rect.Height) / 100f));
            drawList.PrimReserve(6, 6);
            WriteVertex(drawList, a, center, size, transform);
            WriteVertex(drawList, b, center, size, transform);
            WriteVertex(drawList, c, center, size, transform);
            WriteVertex(drawList, a, center, size, transform);
            WriteVertex(drawList, c, center, size, transform);
            WriteVertex(drawList, d, center, size, transform);
            drawList.AddQuad(a, b, c, d, ring, Metrics.Stroke.Thin * scale);
        }

        var diagonal = MathF.Sqrt(size.X * size.X + size.Y * size.Y) / MathF.Sqrt(2f);
        for (var index = 0; index < mask.Circles.Length; index++)
        {
            var circle = mask.Circles[index];
            var circleCenter = Map(center, size, transform, new Vector2(circle.X / 100f, circle.Y / 100f));
            var radius = circle.Radius / 100f * diagonal;
            DrawTexturedDisc(drawList, circleCenter, radius, center, size, transform);
            drawList.AddCircle(circleCenter, radius, ring, CircleSegments, Metrics.Stroke.Ring * scale);
        }

        drawList.PopTextureID();
        drawList.PopClipRect();
    }

    private static void DrawTexturedDisc(ImDrawListPtr drawList, Vector2 discCenter, float radius, Vector2 center,
        Vector2 size, in ImageTransform transform)
    {
        drawList.PrimReserve(CircleSegments * 3, CircleSegments * 3);
        var step = MathF.PI * 2f / CircleSegments;
        for (var index = 0; index < CircleSegments; index++)
        {
            var angleA = index * step;
            var angleB = (index + 1) * step;
            var pointA = new Vector2(discCenter.X + MathF.Cos(angleA) * radius, discCenter.Y + MathF.Sin(angleA) * radius);
            var pointB = new Vector2(discCenter.X + MathF.Cos(angleB) * radius, discCenter.Y + MathF.Sin(angleB) * radius);
            WriteVertex(drawList, discCenter, center, size, transform);
            WriteVertex(drawList, pointA, center, size, transform);
            WriteVertex(drawList, pointB, center, size, transform);
        }
    }

    private static void WriteVertex(ImDrawListPtr drawList, Vector2 position, Vector2 center, Vector2 size,
        in ImageTransform transform)
    {
        var local = transform.Inverse(position - center);
        var uv = new Vector2(local.X / size.X + 0.5f, local.Y / size.Y + 0.5f);
        drawList.PrimVtx(position, uv, 0xFFFFFFFFu);
    }

    private static Vector2 DrawnSize(Rect frame, in ImageTransform transform) =>
        transform.SwapsAxes ? new Vector2(frame.Height, frame.Width) : frame.Size;

    private static Vector2 Map(Vector2 center, Vector2 size, in ImageTransform transform, Vector2 uv)
    {
        var offset = new Vector2((uv.X - 0.5f) * size.X, (uv.Y - 0.5f) * size.Y);
        return center + transform.Forward(offset);
    }

    public static Vector4 PlaceholderFor(PhoneTheme theme) => Palette.WithAlpha(theme.TextMuted, 0.12f);
}
