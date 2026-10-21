using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PlayingCards
{
    public const int RankCount = 13;

    public const int SuitCount = 4;

    public const int DeckSize = RankCount * SuitCount;

    public const int FaceDown = -1;

    public const int Spades = 0;

    public const int Hearts = 1;

    public const int Diamonds = 2;

    public const int Clubs = 3;

    public const float AspectRatio = 1.4f;

    public const float RoundingFraction = 0.12f;

    private static readonly Vector4 Red = new(0.88f, 0.22f, 0.26f, 1f);
    private static readonly Vector4 Black = new(0.14f, 0.15f, 0.19f, 1f);
    private static readonly Vector4 Face = new(0.97f, 0.97f, 0.98f, 1f);
    private static readonly Vector4 BackFill = new(0.20f, 0.26f, 0.52f, 1f);
    private static readonly Vector4 BackInner = new(0.32f, 0.40f, 0.74f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.22f);
    private static readonly Vector4 FaceStroke = new(0f, 0f, 0f, 0.12f);
    private static readonly Vector4 SlotFill = new(1f, 1f, 1f, 0.04f);
    private static readonly Vector4 SlotStroke = new(1f, 1f, 1f, 0.16f);

    public static int Rank(int card) => card % RankCount;

    public static int Suit(int card) => card / RankCount;

    public static bool IsRed(int card) => Suit(card) == Hearts || Suit(card) == Diamonds;

    public static bool IsCard(int card) => card >= 0 && card < DeckSize;

    public static int Encode(int rank, int suit) => suit * RankCount + rank;

    public static float HeightFor(float width) => width * AspectRatio;

    public static float WidthFor(float height) => height / AspectRatio;

    public static float RoundingFor(float width) => width * RoundingFraction;

    private static readonly string[] RankLabels =
    {
        "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K",
    };

    public static string RankLabel(int rank) => RankLabels[rank];

    public static void DrawFace(ImDrawListPtr drawList, in Rect rect, int card, float rounding, float scale,
        bool shadowed)
    {
        if (shadowed)
        {
            DrawShadow(drawList, rect, rounding, scale);
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(Face));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(FaceStroke), 1f * scale);
        if (rect.Width <= 1f)
        {
            return;
        }

        var suit = Suit(card);
        var color = IsRed(card) ? Red : Black;
        var labelScale = MathF.Max(0.5f, MathF.Min(0.9f, rect.Width / (40f * scale)));
        var label = RankLabel(Rank(card));
        var corner = new Vector2(rect.Min.X + rect.Width * 0.18f, rect.Min.Y + rect.Height * 0.16f);
        DrawRankLabel(drawList, corner, label, color, labelScale);
        DrawSuit(drawList, new Vector2(corner.X, corner.Y + rect.Height * 0.2f), rect.Width * 0.1f, suit, color);
        DrawSuit(drawList, new Vector2(rect.Center.X, rect.Center.Y + rect.Height * 0.08f), rect.Width * 0.24f, suit,
            color);
    }

    public static void DrawBack(ImDrawListPtr drawList, in Rect rect, float rounding, float scale, bool shadowed)
    {
        if (shadowed)
        {
            DrawShadow(drawList, rect, rounding, scale);
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(BackFill));
        if (rect.Width <= 1f)
        {
            return;
        }

        var inset = rect.Width * 0.12f;
        var innerMin = rect.Min + new Vector2(inset, inset);
        var innerMax = rect.Max - new Vector2(inset, inset);
        Squircle.Stroke(drawList, innerMin, innerMax, rounding * 0.6f, ImGui.GetColorU32(BackInner), 1.5f * scale);
        drawList.AddCircleFilled(rect.Center, rect.Width * 0.12f, ImGui.GetColorU32(BackInner), 20);
    }

    public static void DrawSlot(ImDrawListPtr drawList, in Rect rect, float rounding, float scale)
    {
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(SlotFill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(SlotStroke), 1.4f * scale);
    }

    public static void DrawSuit(ImDrawListPtr drawList, Vector2 center, float radius, int suit, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        switch (suit)
        {
            case Diamonds:
                DrawDiamond(drawList, center, radius, packed);
                break;
            case Hearts:
                DrawHeart(drawList, center, radius, packed);
                break;
            case Spades:
                DrawSpade(drawList, center, radius, packed);
                break;
            default:
                DrawClub(drawList, center, radius, packed);
                break;
        }
    }

    // Typography.DrawCentered wraps against the window content region, which splits "10" down the middle on cards
    // sitting near the window edge.
    private static void DrawRankLabel(ImDrawListPtr drawList, Vector2 center, string label, Vector4 color, float scale)
    {
        using (Plugin.Fonts.Push(scale, FontWeight.Bold))
        {
            Plugin.Fonts.NoticeText(label);
            var size = ImGui.CalcTextSize(label);
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), center - size * 0.5f, ImGui.GetColorU32(color),
                label);
        }
    }

    private static void DrawShadow(ImDrawListPtr drawList, in Rect rect, float rounding, float scale)
    {
        var drop = new Vector2(0f, 1.5f * scale);
        Squircle.Fill(drawList, rect.Min + drop, rect.Max + drop, rounding, ImGui.GetColorU32(Shadow));
    }

    private static void DrawDiamond(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        Span<Vector2> points = stackalloc Vector2[4]
        {
            new(center.X, center.Y - radius), new(center.X + radius * 0.72f, center.Y),
            new(center.X, center.Y + radius), new(center.X - radius * 0.72f, center.Y),
        };
        FillConvex(drawList, packed, points);
    }

    private static void DrawHeart(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        var lobe = radius * 0.5f;
        drawList.AddCircleFilled(new Vector2(center.X - lobe, center.Y - radius * 0.2f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X + lobe, center.Y - radius * 0.2f), lobe, packed, 20);
        Span<Vector2> triangle = stackalloc Vector2[3]
        {
            new(center.X - radius * 0.98f, center.Y - radius * 0.08f),
            new(center.X + radius * 0.98f, center.Y - radius * 0.08f), new(center.X, center.Y + radius),
        };
        FillConvex(drawList, packed, triangle);
    }

    private static void DrawSpade(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        var lobe = radius * 0.5f;
        drawList.AddCircleFilled(new Vector2(center.X - lobe, center.Y + radius * 0.2f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X + lobe, center.Y + radius * 0.2f), lobe, packed, 20);
        Span<Vector2> triangle = stackalloc Vector2[3]
        {
            new(center.X - radius * 0.98f, center.Y + radius * 0.08f),
            new(center.X + radius * 0.98f, center.Y + radius * 0.08f), new(center.X, center.Y - radius),
        };
        FillConvex(drawList, packed, triangle);
        DrawStem(drawList, center, radius, packed);
    }

    private static void DrawClub(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        var lobe = radius * 0.46f;
        drawList.AddCircleFilled(new Vector2(center.X, center.Y - radius * 0.42f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X - radius * 0.52f, center.Y + radius * 0.18f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X + radius * 0.52f, center.Y + radius * 0.18f), lobe, packed, 20);
        DrawStem(drawList, center, radius, packed);
    }

    private static void DrawStem(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        Span<Vector2> stem = stackalloc Vector2[4]
        {
            new(center.X - radius * 0.12f, center.Y + radius * 0.18f),
            new(center.X + radius * 0.12f, center.Y + radius * 0.18f),
            new(center.X + radius * 0.26f, center.Y + radius * 0.98f),
            new(center.X - radius * 0.26f, center.Y + radius * 0.98f),
        };
        FillConvex(drawList, packed, stem);
    }

    private static void FillConvex(ImDrawListPtr drawList, uint color, ReadOnlySpan<Vector2> points)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathFillConvex(color);
    }
}
