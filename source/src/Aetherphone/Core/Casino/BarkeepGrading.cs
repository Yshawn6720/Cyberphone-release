namespace Aetherphone.Core.Casino;

internal static class BarkeepGrading
{
    public const int PerfectGrade = 100;

    public const int GoodGrade = 70;

    public const int RoughGrade = 40;

    public const int MissGrade = 0;

    public const float PerfectWithin = 0.18f;

    public const float GoodWithin = 0.45f;

    public const float RoughWithin = 0.80f;

    public const float PourFullScale = 0.30f;

    public const float ShakeFullScaleSeconds = 0.42f;

    public const float LayerFullScale = 0.55f;

    public const float GarnishFullScale = 0.60f;

    public static int Quantize(float normalizedError)
    {
        if (float.IsNaN(normalizedError))
        {
            return MissGrade;
        }

        var error = MathF.Abs(normalizedError);
        if (error <= PerfectWithin)
        {
            return PerfectGrade;
        }

        if (error <= GoodWithin)
        {
            return GoodGrade;
        }

        if (error <= RoughWithin)
        {
            return RoughGrade;
        }

        return MissGrade;
    }

    public static int GradePour(float fillLevel, float bandCenter)
    {
        return Quantize((fillLevel - bandCenter) / PourFullScale);
    }

    public static int GradeShake(float averageBeatErrorSeconds)
    {
        return Quantize(averageBeatErrorSeconds / ShakeFullScaleSeconds);
    }

    public static int GradeLayer(float averageLevelOffset)
    {
        return Quantize(averageLevelOffset / LayerFullScale);
    }

    public static int GradeGarnish(float markerOffset)
    {
        return Quantize(markerOffset / GarnishFullScale);
    }
}
