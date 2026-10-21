using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Tables;

internal static class BlackjackDealChoreography
{
    public const float StaggerSeconds = 0.09f;

    public const float TravelSeconds = 0.30f;

    public const float FlipFraction = 0.60f;

    public const float ArcHeight = 26f;

    public const float RevealSeconds = 0.28f;

    public static bool RevealFaceUp(float progress)
    {
        return progress >= 0.5f;
    }

    public static float RevealScaleX(float progress)
    {
        if (progress <= 0f || progress >= 1f)
        {
            return 1f;
        }

        return progress < 0.5f ? 1f - progress * 2f : progress * 2f - 1f;
    }

    public static float Travel(float elapsedSeconds, int cardIndex)
    {
        if (cardIndex < 0)
        {
            return 1f;
        }

        var started = elapsedSeconds - cardIndex * StaggerSeconds;
        if (started <= 0f)
        {
            return 0f;
        }

        var travel = started / TravelSeconds;
        return travel >= 1f ? 1f : travel;
    }

    public static bool FaceUp(float travel)
    {
        return travel >= FlipFraction;
    }

    public static float FlipScaleX(float travel)
    {
        if (travel <= 0f)
        {
            return 1f;
        }

        if (travel < FlipFraction)
        {
            return 1f - travel / FlipFraction;
        }

        var opening = (travel - FlipFraction) / (1f - FlipFraction);
        return opening >= 1f ? 1f : opening;
    }

    public static float Duration(int cardCount)
    {
        if (cardCount <= 0)
        {
            return 0f;
        }

        return (cardCount - 1) * StaggerSeconds + TravelSeconds;
    }

    public static Vector2 Position(Vector2 from, Vector2 to, float travel, float scale)
    {
        var eased = 1f - (1f - travel) * (1f - travel);
        var point = Vector2.Lerp(from, to, eased);
        point.Y -= MathF.Sin(eased * MathF.PI) * ArcHeight * scale;
        return point;
    }

    public static Rect CardRect(Vector2 center, float width, float travel)
    {
        var height = Windows.Components.PlayingCards.HeightFor(width);
        var halfWidth = width * 0.5f * FlipScaleX(travel);
        var halfHeight = height * 0.5f;
        return new Rect(new Vector2(center.X - halfWidth, center.Y - halfHeight),
            new Vector2(center.X + halfWidth, center.Y + halfHeight));
    }
}
