using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum SeatPhase
{
    Empty,
    Sitting,
    Betting,
    Acting,
    Waiting,
    Away,
    Out,
}

internal readonly record struct SeatView(
    int SeatIndex,
    string DisplayName,
    string AvatarUrl,
    string FrameId,
    long Stack,
    long Bet,
    SeatPhase Phase,
    bool IsMe,
    bool Connected);

internal readonly record struct SeatRingStyle(
    Vector4 Accent,
    Vector4 TitleInk,
    Vector4 BodyInk,
    Vector4 MutedInk,
    Vector4 Surface,
    Vector4 Money);

internal static class SeatRing
{
    public const float ArcStartDegrees = 160f;

    public const float ArcEndDegrees = 20f;

    public const float BottomDegrees = 90f;

    public const float CenterHeightFraction = 0.44f;

    public const float RadiusXFraction = 0.38f;

    public const float RadiusYFraction = 0.32f;

    public const float DealerHeightFraction = 0.10f;

    public const float PuckRadius = 24f;

    public const float BetPushFactor = 1.5f;

    public const float HandPushFactor = 2.9f;

    private const float RingThickness = 2.4f;

    public static int DisplaySlot(int seatIndex, int seatCount, int mySeatIndex)
    {
        if (seatCount <= 0)
        {
            return 0;
        }

        if (mySeatIndex < 0 || mySeatIndex >= seatCount)
        {
            return seatIndex;
        }

        var center = seatCount / 2;
        var slot = (seatIndex - mySeatIndex + center) % seatCount;
        return slot < 0 ? slot + seatCount : slot;
    }

    public static float SlotDegrees(int seatCount, int slot)
    {
        if (seatCount <= 1)
        {
            return BottomDegrees;
        }

        var span = (ArcEndDegrees - ArcStartDegrees) / (seatCount - 1);
        return ArcStartDegrees + span * slot;
    }

    public static Vector2 EllipseCenter(in Rect arena)
    {
        return new Vector2(arena.Center.X, arena.Min.Y + arena.Height * CenterHeightFraction);
    }

    public static Vector2 DealerAnchor(in Rect arena)
    {
        return new Vector2(arena.Center.X, arena.Min.Y + arena.Height * DealerHeightFraction);
    }

    public static Vector2 SlotCenter(in Rect arena, int seatCount, int slot)
    {
        var center = EllipseCenter(arena);
        var radians = SlotDegrees(seatCount, slot) * (MathF.PI / 180f);
        return new Vector2(center.X + MathF.Cos(radians) * arena.Width * RadiusXFraction,
            center.Y + MathF.Sin(radians) * arena.Height * RadiusYFraction);
    }

    public static Vector2 Pushed(Vector2 seatCenter, Vector2 towards, float radius, float factor)
    {
        var offset = towards - seatCenter;
        var length = offset.Length();
        if (length <= 0.0001f)
        {
            return new Vector2(seatCenter.X, seatCenter.Y - radius * factor);
        }

        return seatCenter + offset / length * (radius * factor);
    }

    public static void Layout(in Rect arena, int seatCount, int mySeatIndex, Span<Vector2> centers)
    {
        for (var seatIndex = 0; seatIndex < seatCount && seatIndex < centers.Length; seatIndex++)
        {
            centers[seatIndex] = SlotCenter(arena, seatCount, DisplaySlot(seatIndex, seatCount, mySeatIndex));
        }
    }

    public static int HitTest(in Rect arena, int seatCount, int mySeatIndex, float hitRadius, Vector2 point)
    {
        var squared = hitRadius * hitRadius;
        for (var seatIndex = 0; seatIndex < seatCount; seatIndex++)
        {
            var center = SlotCenter(arena, seatCount, DisplaySlot(seatIndex, seatCount, mySeatIndex));
            var offset = point - center;
            if (offset.X * offset.X + offset.Y * offset.Y <= squared)
            {
                return seatIndex;
            }
        }

        return -1;
    }

    public static int Draw(ImDrawListPtr drawList, in Rect arena, ReadOnlySpan<SeatView> seats, int mySeatIndex,
        float scale, in SeatRingStyle style, PhoneTheme theme, RemoteImageCache images, LodestoneService lodestone,
        long remainingMilliseconds, int windowSeconds)
    {
        var radius = PuckRadius * scale;
        var tapped = -1;
        var ellipseCenter = EllipseCenter(arena);
        for (var index = 0; index < seats.Length; index++)
        {
            ref readonly var seat = ref seats[index];
            var center = SlotCenter(arena, seats.Length, DisplaySlot(seat.SeatIndex, seats.Length, mySeatIndex));
            if (DrawSeat(drawList, seat, center, ellipseCenter, radius, scale, style, theme, images, lodestone,
                    remainingMilliseconds, windowSeconds))
            {
                tapped = seat.SeatIndex;
            }
        }

        return tapped;
    }

    private static bool DrawSeat(ImDrawListPtr drawList, in SeatView seat, Vector2 center, Vector2 ellipseCenter,
        float radius, float scale, in SeatRingStyle style, PhoneTheme theme, RemoteImageCache images,
        LodestoneService lodestone, long remainingMilliseconds, int windowSeconds)
    {
        var hovered = CircleHovered(center, radius);
        if (seat.Phase == SeatPhase.Empty)
        {
            DrawEmptySeat(drawList, center, radius, scale, style, hovered);
            return UiInteract.Click(center - new Vector2(radius, radius), center + new Vector2(radius, radius),
                hovered);
        }

        var dimmed = seat.Phase == SeatPhase.Away || seat.Phase == SeatPhase.Out || !seat.Connected;
        var alpha = dimmed ? 0.45f : 1f;
        AvatarView.DrawRemote(drawList, center, radius, theme, seat.DisplayName, string.Empty, seat.AvatarUrl,
            images, lodestone, 1f, 32, alpha, Frames.Of(seat.FrameId));
        if (seat.Phase == SeatPhase.Acting)
        {
            TurnTimerRing.Draw(drawList, center, radius + 4f * scale, remainingMilliseconds, windowSeconds,
                style.Accent, scale);
        }
        else if (seat.IsMe)
        {
            drawList.AddCircle(center, radius + 1.5f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(style.Accent, 0.75f)), 32, RingThickness * scale);
        }

        var name = Typography.FitText(seat.DisplayName, radius * 2.6f, TextStyles.Caption2);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius + 8f * scale), name,
            dimmed ? style.MutedInk : style.BodyInk, TextStyles.Caption2);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius + 21f * scale),
            NumberText.Group(seat.Stack), dimmed ? style.MutedInk : style.Money, TextStyles.Caption2);

        if (!seat.Connected)
        {
            drawList.AddCircleFilled(new Vector2(center.X + radius * 0.7f, center.Y - radius * 0.7f), 3.4f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(style.MutedInk, 0.45f + 0.4f * Pulse.Wave(Pulse.Breath))), 12);
        }

        if (seat.Bet > 0)
        {
            ChipStack.Draw(drawList, Pushed(center, ellipseCenter, radius, BetPushFactor), seat.Bet, scale,
                style.Money);
        }

        return UiInteract.Click(center - new Vector2(radius, radius), center + new Vector2(radius, radius), hovered);
    }

    private static void DrawEmptySeat(ImDrawListPtr drawList, Vector2 center, float radius, float scale,
        in SeatRingStyle style, bool hovered)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(style.Surface, 0.35f)), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(style.MutedInk, hovered ? 0.7f : 0.35f)),
            32, Metrics.Stroke.Thin * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, center, Loc.T(L.Casino.SeatOpen), style.MutedInk, TextStyles.Caption2);
    }

    private static bool CircleHovered(Vector2 center, float radius)
    {
        var offset = ImGui.GetMousePos() - center;
        if (offset.LengthSquared() > radius * radius)
        {
            return false;
        }

        var corner = new Vector2(radius, radius);
        return UiInteract.Hover(center - corner, center + corner);
    }
}
