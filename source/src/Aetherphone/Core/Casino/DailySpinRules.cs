namespace Aetherphone.Core.Casino;

internal static class DailySpinRules
{
    public const int SegmentCount = 16;

    public const long TotalAward = 280;

    public const long TopAward = 60;

    public static readonly long[] Awards =
    {
        5, 15, 10, 30,
        5, 20, 10, 45,
        5, 15, 10, 60,
        5, 20, 10, 15,
    };

    public static bool IsSegment(int segment)
    {
        return segment >= 0 && segment < SegmentCount;
    }

    public static long AwardOf(int segment)
    {
        return IsSegment(segment) ? Awards[segment] : 0;
    }

    public static bool IsTopAward(int segment)
    {
        return AwardOf(segment) >= TopAward;
    }
}
