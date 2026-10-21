namespace Aetherphone.Core.Animation;

internal readonly struct LayerTransform
{
    private const uint ColorMask = 0x00FFFFFF;
    private const int AlphaShift = 24;
    private const float UnboundedExtent = 1e6f;
    private const float MinimumScale = 1e-4f;

    private static readonly Rect Unbounded =
        new(new Vector2(-UnboundedExtent, -UnboundedExtent), new Vector2(UnboundedExtent, UnboundedExtent));

    public readonly float Scale;
    public readonly Vector2 Anchor;
    public readonly Vector2 Target;
    public readonly Rect Clip;
    public readonly float Alpha;

    private readonly float cosine;
    private readonly float sine;

    public LayerTransform(float scale, Vector2 anchor, Vector2 target, Rect clip, float alpha)
        : this(scale, anchor, target, clip, alpha, 0f)
    {
    }

    public LayerTransform(float scale, Vector2 anchor, Vector2 target, Rect clip, float alpha, float radians)
    {
        Scale = scale;
        Anchor = anchor;
        Target = target;
        Clip = clip;
        Alpha = Math.Clamp(alpha, 0f, 1f);
        cosine = MathF.Cos(radians);
        sine = MathF.Sin(radians);
    }

    public static LayerTransform Identity(Rect clip) => new(1f, Vector2.Zero, Vector2.Zero, clip, 1f);

    public static LayerTransform Translate(Vector2 offset, Rect clip) => new(1f, Vector2.Zero, offset, clip, 1f);

    public static LayerTransform Fade(float alpha) => new(1f, Vector2.Zero, Vector2.Zero, Unbounded, alpha);

    public static LayerTransform Turn(Vector2 pivot, float radians, float scale, Rect clip) =>
        new(scale, pivot, pivot, clip, 1f, radians);

    public static LayerTransform ScaleAbout(Vector2 pivot, float scale, Rect clip, float alpha = 1f) =>
        new(scale, pivot, pivot, clip, alpha);

    public static LayerTransform Fit(Rect source, Rect target, Rect clip, float alpha = 1f)
    {
        var scale = source.Width > 0f ? target.Width / source.Width : 1f;
        return new LayerTransform(scale, source.Min, target.Min, clip, alpha);
    }

    public static LayerTransform Cover(Rect source, Rect target, Rect clip, float alpha = 1f)
    {
        var widthScale = source.Width > 0f ? target.Width / source.Width : 1f;
        var heightScale = source.Height > 0f ? target.Height / source.Height : 1f;
        return new LayerTransform(MathF.Max(widthScale, heightScale), source.Center, target.Center, clip, alpha);
    }

    public bool IsRotated => sine != 0f;

    public bool IsIdentity => Scale == 1f && Anchor == Target && Alpha >= 1f && !IsRotated && cosine == 1f;

    public Vector2 Map(Vector2 point)
    {
        var local = (point - Anchor) * Scale;
        return Target + new Vector2(local.X * cosine - local.Y * sine, local.X * sine + local.Y * cosine);
    }

    public Rect Map(Rect rect) => new(Map(rect.Min), Map(rect.Max));

    public Vector2 Unmap(Vector2 point)
    {
        var local = point - Target;
        var unrotated = new Vector2(local.X * cosine + local.Y * sine, local.Y * cosine - local.X * sine);
        return Anchor + unrotated / MathF.Max(Scale, MinimumScale);
    }

    public Vector4 MapClip(Vector4 clip)
    {
        var first = Map(new Vector2(clip.X, clip.Y));
        var second = Map(new Vector2(clip.Z, clip.W));
        var min = Vector2.Min(first, second);
        var max = Vector2.Max(first, second);
        if (IsRotated)
        {
            var third = Map(new Vector2(clip.Z, clip.Y));
            var fourth = Map(new Vector2(clip.X, clip.W));
            min = Vector2.Min(min, Vector2.Min(third, fourth));
            max = Vector2.Max(max, Vector2.Max(third, fourth));
        }

        var left = MathF.Max(min.X, Clip.Min.X);
        var top = MathF.Max(min.Y, Clip.Min.Y);
        var right = MathF.Min(max.X, Clip.Max.X);
        var bottom = MathF.Min(max.Y, Clip.Max.Y);
        if (right <= left || bottom <= top)
        {
            return new Vector4(left, top, left, top);
        }

        return new Vector4(left, top, right, bottom);
    }

    public uint MapColor(uint color)
    {
        if (Alpha >= 1f)
        {
            return color;
        }

        var alpha = (uint)((color >> AlphaShift) * Alpha + 0.5f);
        return (color & ColorMask) | (alpha << AlphaShift);
    }
}
