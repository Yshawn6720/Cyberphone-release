using System.Numerics;

namespace Aetherphone.Tests;

internal static class OklabHue
{
    public static float Degrees(Vector4 color)
    {
        var (a, b) = ToAb(color);
        var hue = MathF.Atan2(b, a) * 180f / MathF.PI;
        return hue < 0f ? hue + 360f : hue;
    }

    public static float Chroma(Vector4 color)
    {
        var (a, b) = ToAb(color);
        return MathF.Sqrt(a * a + b * b);
    }

    public static float Distance(Vector4 first, Vector4 second)
    {
        var delta = MathF.Abs(Degrees(first) - Degrees(second));
        return MathF.Min(delta, 360f - delta);
    }

    private static (float A, float B) ToAb(Vector4 color)
    {
        var red = Linear(color.X);
        var green = Linear(color.Y);
        var blue = Linear(color.Z);
        var longWave = MathF.Cbrt(0.4122214708f * red + 0.5363325363f * green + 0.0514459929f * blue);
        var mediumWave = MathF.Cbrt(0.2119034982f * red + 0.6806995451f * green + 0.1073969566f * blue);
        var shortWave = MathF.Cbrt(0.0883024619f * red + 0.2817188376f * green + 0.6299787005f * blue);
        var a = 1.9779984951f * longWave - 2.4285922050f * mediumWave + 0.4505937099f * shortWave;
        var b = 0.0259040371f * longWave + 0.7827717662f * mediumWave - 0.8086757660f * shortWave;
        return (a, b);
    }

    private static float Linear(float channel) =>
        channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
}
