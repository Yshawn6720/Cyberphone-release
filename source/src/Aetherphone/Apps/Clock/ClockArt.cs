using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Clock;

internal static class ClockArt
{
    public const float ControlRadius = 37f;
    public const float SectionGap = 18f;
    public const float BottomPad = 24f;
    public const float MeridiemGap = 3f;
    public const float MeridiemLift = 0.12f;

    public static readonly Vector4 StartGreen = new(0.20f, 0.78f, 0.35f, 1f);
    public static readonly Vector4 PauseOrange = new(1.00f, 0.62f, 0.04f, 1f);
    public static readonly Vector4 NeutralGray = new(0.62f, 0.62f, 0.66f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private const float ControlFillAlpha = 0.24f;
    private const float ControlHoverAlpha = 0.32f;
    private const float ControlDisabledAlpha = 0.45f;
    private const float ControlGapUnits = 3.2f;
    private const float ControlGapStroke = 2.2f;
    private const float ControlLabelFraction = 1.62f;
    private const float LabelLighten = 0.18f;
    private const int MaxPlain = 99;

    private static readonly string[] Pairs = BuildNumbers(true);
    private static readonly string[] Plains = BuildNumbers(false);

    public static string Pair(int value) => Pairs[Math.Clamp(value, 0, MaxPlain)];

    public static string Plain(int value) => Plains[Math.Clamp(value, 0, MaxPlain)];

    private static string[] BuildNumbers(bool padded)
    {
        var numbers = new string[MaxPlain + 1];
        for (var index = 0; index <= MaxPlain; index++)
        {
            numbers[index] = padded ? index.ToString("D2") : index.ToString();
        }

        return numbers;
    }

    public static bool ControlButton(ImDrawListPtr drawList, string id, Vector2 center, float radius, string label,
        Vector4 tint, Vector4 gapInk, bool enabled)
    {
        var scale = UiScale.Current;
        var corner = new Vector2(radius, radius);
        var offset = ImGui.GetMousePos() - center;
        var inside = offset.LengthSquared() <= radius * radius;
        var hovered = enabled && inside && UiInteract.Hover(center - corner, center + corner);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, Motion.PressScaleControl);
        var drawn = radius * grow;
        var alpha = enabled ? 1f : ControlDisabledAlpha;
        var fillAlpha = hovered ? ControlHoverAlpha : ControlFillAlpha;
        drawList.AddCircleFilled(center, drawn, ImGui.GetColorU32(tint with { W = fillAlpha * alpha }), 48);
        drawList.AddCircle(center, drawn - ControlGapUnits * scale * grow, ImGui.GetColorU32(gapInk),
            48, ControlGapStroke * scale);
        var labelInk = Palette.Lighten(tint, LabelLighten) with { W = alpha };
        var fitted = Typography.FitText(label, drawn * ControlLabelFraction, TextStyles.BodyEmphasized);
        Typography.DrawCentered(drawList, center, fitted, labelInk, TextStyles.BodyEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - corner, center + corner, hovered);
    }

    public static float DurationWidth(int hours, int minutes, int seconds, int centis, bool padMinutes,
        in TextStyle style) =>
        Duration(default, false, Vector2.Zero, hours, minutes, seconds, centis, padMinutes, style, default);

    public static float DrawDuration(ImDrawListPtr drawList, Vector2 topLeft, int hours, int minutes, int seconds,
        int centis, bool padMinutes, in TextStyle style, Vector4 ink) =>
        Duration(drawList, true, topLeft, hours, minutes, seconds, centis, padMinutes, style, ink);

    private static float Duration(ImDrawListPtr drawList, bool draw, Vector2 topLeft, int hours, int minutes,
        int seconds, int centis, bool padMinutes, in TextStyle style, Vector4 ink)
    {
        var cursor = topLeft;
        if (hours > 0)
        {
            cursor.X += Piece(drawList, draw, cursor, Plain(hours), style, ink);
            cursor.X += Piece(drawList, draw, cursor, ":", style, ink);
            cursor.X += Piece(drawList, draw, cursor, Pair(minutes), style, ink);
        }
        else
        {
            cursor.X += Piece(drawList, draw, cursor, padMinutes ? Pair(minutes) : Plain(minutes), style, ink);
        }

        cursor.X += Piece(drawList, draw, cursor, ":", style, ink);
        cursor.X += Piece(drawList, draw, cursor, Pair(seconds), style, ink);
        if (centis >= 0)
        {
            cursor.X += Piece(drawList, draw, cursor, Loc.Culture.NumberFormat.NumberDecimalSeparator, style, ink);
            cursor.X += Piece(drawList, draw, cursor, Pair(centis), style, ink);
        }

        return cursor.X - topLeft.X;
    }

    private static float Piece(ImDrawListPtr drawList, bool draw, Vector2 position, string text, in TextStyle style,
        Vector4 ink) =>
        draw ? WidgetText.Tabular(drawList, position, text, ink, style) : WidgetText.TabularWidth(text, style);

    public static float TimeWidth(int hour, int minute, bool twentyFour, in TextStyle big, in TextStyle small,
        string? suffix = null) =>
        Time(default, false, Vector2.Zero, hour, minute, twentyFour, big, small, default, default, suffix);

    public static float DrawTime(ImDrawListPtr drawList, Vector2 topLeft, int hour, int minute, bool twentyFour,
        in TextStyle big, in TextStyle small, Vector4 ink, Vector4 smallInk, string? suffix = null) =>
        Time(drawList, true, topLeft, hour, minute, twentyFour, big, small, ink, smallInk, suffix);

    private static float Time(ImDrawListPtr drawList, bool draw, Vector2 topLeft, int hour, int minute,
        bool twentyFour, in TextStyle big, in TextStyle small, Vector4 ink, Vector4 smallInk, string? suffix)
    {
        var cursor = topLeft;
        var hourText = twentyFour ? Pair(hour) : TimeText.HourLabel(hour);
        cursor.X += Piece(drawList, draw, cursor, hourText, big, ink);
        cursor.X += Piece(drawList, draw, cursor, ":", big, ink);
        cursor.X += Piece(drawList, draw, cursor, twentyFour ? Pair(minute) : TimeText.MinuteLabel(minute), big, ink);
        var tail = suffix ?? (twentyFour || TimeText.Use24Hour ? null : TimeText.MeridiemLabel(hour >= 12));
        if (tail is null)
        {
            return cursor.X - topLeft.X;
        }

        var scale = UiScale.Current;
        cursor.X += MeridiemGap * scale;
        var bigHeight = Typography.LineHeight(big);
        var smallHeight = Typography.LineHeight(small);
        var smallTop = cursor.Y + bigHeight - smallHeight - bigHeight * MeridiemLift;
        if (draw)
        {
            Typography.Draw(drawList, new Vector2(cursor.X, smallTop), tail, smallInk, small);
        }

        cursor.X += Typography.Measure(tail, small).X;
        return cursor.X - topLeft.X;
    }

    public static void Advance(Vector2 origin, float width, float height, float gapUnits, float scale)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, height + gapUnits * scale - ImGui.GetStyle().ItemSpacing.Y)));
    }

    public static void MinusBadge(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 danger)
    {
        var scale = UiScale.Current;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(danger), 24);
        var arm = radius * 0.5f;
        drawList.AddLine(center - new Vector2(arm, 0f), center + new Vector2(arm, 0f), ImGui.GetColorU32(White),
            2f * scale);
    }

    public static void Grip(ImDrawListPtr drawList, Vector2 center, float width, Vector4 ink)
    {
        var scale = UiScale.Current;
        var color = ImGui.GetColorU32(ink);
        var half = width * 0.5f;
        for (var line = -1; line <= 1; line++)
        {
            var y = center.Y + line * 5f * scale;
            drawList.AddLine(new Vector2(center.X - half, y), new Vector2(center.X + half, y), color, 1.6f * scale);
        }
    }
}
