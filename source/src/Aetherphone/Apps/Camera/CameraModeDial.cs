using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Camera;

internal sealed class CameraModeDial
{
    private const float LabelGap = 26f;
    private const float RowHalfHeight = 16f;
    private const float ColumnRowHeight = 32f;
    private const float ColumnHalfWidth = 44f;
    private const float SwipeThreshold = 28f;
    private const float MaxFrameSeconds = 0.1f;
    private static readonly Vector4 Idle = new(1f, 1f, 1f, 0.78f);

    private readonly LocString[] modes;
    private readonly string[] anchors;
    private readonly float[] widths;
    private readonly float[] centers;
    private Spring position;
    private bool settled;
    private bool pressed;
    private Vector2 pressOrigin;

    public CameraModeDial(LocString[] modes, string[] anchors)
    {
        this.modes = modes;
        this.anchors = anchors;
        widths = new float[modes.Length];
        centers = new float[modes.Length];
    }

    public void Snap(int selected)
    {
        position.SnapTo(selected);
        settled = true;
    }

    public int DrawRow(ImDrawListPtr drawList, Rect screen, float centerY, int selected, float scale)
    {
        Measure(scale);
        var travel = Offset(Step(selected));
        var row = new Rect(new Vector2(screen.Min.X, centerY - RowHalfHeight * scale),
            new Vector2(screen.Max.X, centerY + RowHalfHeight * scale));
        var result = selected;
        drawList.PushClipRect(row.Min, row.Max, true);
        for (var index = 0; index < modes.Length; index++)
        {
            var x = screen.Center.X + centers[index] - travel;
            var labelCenter = new Vector2(x, centerY);
            var half = widths[index] * 0.5f + LabelGap * 0.4f * scale;
            var hit = new Rect(new Vector2(x - half, row.Min.Y), new Vector2(x + half, row.Max.Y));
            if (DrawLabel(drawList, index, labelCenter, hit, selected))
            {
                result = index;
            }
        }

        drawList.PopClipRect();
        return Swipe(row, result, true, scale);
    }

    public int DrawColumn(ImDrawListPtr drawList, float centerX, float top, float bottom, int selected, float scale)
    {
        Measure(scale);
        var travel = Step(selected);
        var span = MathF.Max(bottom - top, 0f);
        var rowHeight = MathF.Min(ColumnRowHeight * scale, span / Math.Max(1, modes.Length));
        var middle = (top + bottom) * 0.5f;
        var column = new Rect(new Vector2(centerX - ColumnHalfWidth * scale, top),
            new Vector2(centerX + ColumnHalfWidth * scale, bottom));
        var result = selected;
        drawList.PushClipRect(column.Min, column.Max, true);
        for (var index = 0; index < modes.Length; index++)
        {
            var y = middle + (index - travel) * rowHeight;
            var hit = new Rect(new Vector2(column.Min.X, y - rowHeight * 0.5f),
                new Vector2(column.Max.X, y + rowHeight * 0.5f));
            if (DrawLabel(drawList, index, new Vector2(centerX, y), hit, selected))
            {
                result = index;
            }
        }

        drawList.PopClipRect();
        return Swipe(column, result, false, scale);
    }

    private bool DrawLabel(ImDrawListPtr drawList, int index, Vector2 center, Rect hit, int selected)
    {
        var active = index == selected;
        Typography.DrawCentered(drawList, center, Loc.T(modes[index]), active ? CameraChrome.Yellow : Idle,
            TextStyles.FootnoteEmphasized);
        UiAnchors.Report(anchors[index], hit);
        if (active)
        {
            return false;
        }

        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(hit.Min, hit.Max, hovered, false);
    }

    private int Swipe(Rect area, int selected, bool horizontal, float scale)
    {
        if (UiInteract.Hover(area.Min, area.Max))
        {
            UiInteract.ReportGestureSurface();
        }

        var mouse = ImGui.GetMousePos();
        if (!pressed)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(area.Min, area.Max))
            {
                pressed = true;
                pressOrigin = mouse;
            }

            return selected;
        }

        var travel = horizontal ? mouse.X - pressOrigin.X : mouse.Y - pressOrigin.Y;
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (MathF.Abs(travel) > SwipeThreshold * scale)
            {
                UiInteract.CancelPendingTap();
            }

            return selected;
        }

        pressed = false;
        if (MathF.Abs(travel) <= SwipeThreshold * scale)
        {
            return selected;
        }

        var step = travel < 0f ? 1 : -1;
        return Math.Clamp(selected + step, 0, modes.Length - 1);
    }

    private float Step(int selected)
    {
        if (!settled)
        {
            Snap(selected);
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        return position.Step(selected, Motion.PageSettle, delta);
    }

    private float Offset(float value)
    {
        var lower = Math.Clamp((int)MathF.Floor(value), 0, modes.Length - 1);
        var upper = Math.Clamp(lower + 1, 0, modes.Length - 1);
        var fraction = Math.Clamp(value - lower, 0f, 1f);
        return centers[lower] + (centers[upper] - centers[lower]) * fraction;
    }

    private void Measure(float scale)
    {
        var gap = LabelGap * scale;
        var cursor = 0f;
        for (var index = 0; index < modes.Length; index++)
        {
            widths[index] = Typography.Measure(Loc.T(modes[index]), TextStyles.FootnoteEmphasized).X;
            if (index > 0)
            {
                cursor += widths[index - 1] * 0.5f + gap + widths[index] * 0.5f;
            }

            centers[index] = cursor;
        }
    }
}
