namespace Aetherphone.Core.Photos;

internal static class PhotoPaging
{
    public const float DistanceFraction = 0.22f;
    public const float FlingSpeed = 700f;
    public const float EdgeResistance = 0.35f;

    public static int Settle(float offset, float velocity, float pageWidth, bool hasPrevious, bool hasNext,
        float scale)
    {
        var fling = FlingSpeed * MathF.Max(scale, 0.0001f);
        var distance = pageWidth * DistanceFraction;
        if (hasNext && (offset <= -distance || (velocity <= -fling && offset < 0f)))
        {
            return 1;
        }

        if (hasPrevious && (offset >= distance || (velocity >= fling && offset > 0f)))
        {
            return -1;
        }

        return 0;
    }

    public static float Resist(float offset, bool hasPrevious, bool hasNext)
    {
        if ((offset > 0f && !hasPrevious) || (offset < 0f && !hasNext))
        {
            return offset * EdgeResistance;
        }

        return offset;
    }
}
