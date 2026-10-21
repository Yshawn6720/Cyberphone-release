using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Clock;

internal readonly record struct WheelStyle(Vector4 Ink, Vector4 Muted, TextStyle Text);

internal sealed class ClockWheel
{
    public const float RowUnits = 34f;
    public const int VisibleRows = 5;
    public const float HeightUnits = RowUnits * VisibleRows;

    private const int DrawReach = 3;
    private const float Curvature = 0.36f;
    private const float FadeReach = 2.7f;
    private const float SnapSeconds = 0.12f;
    private const float TapSlopUnits = 4f;
    private const float MaxFrameSeconds = 0.1f;

    private readonly string id;
    private Spring position;
    private float target;
    private bool primed;
    private bool dragging;
    private float pressY;
    private float pressPosition;
    private float travel;
    private int lastTick;

    public ClockWheel(string id)
    {
        this.id = id;
    }

    public bool Active => dragging;

    public bool Hovered { get; private set; }

    public void Snap(int value)
    {
        position.SnapTo(value);
        target = value;
        lastTick = value;
        primed = true;
    }

    public int Draw(ImDrawListPtr drawList, Rect column, int value, IReadOnlyList<string> labels, bool wrap,
        in WheelStyle style)
    {
        var count = labels.Count;
        if (count == 0)
        {
            return value;
        }

        var scale = UiScale.Current;
        var rowHeight = RowUnits * scale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        if (!primed)
        {
            Snap(value);
        }

        var mouse = ImGui.GetMousePos();
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(column.Min);
        ImGui.InvisibleButton($"##clockWheel{id}", column.Size);
        var hovered = ImGui.IsItemHovered() && UiInteract.Hover(column.Min, column.Max);
        var activated = hovered && ImGui.IsItemActivated();
        ImGui.SetCursorScreenPos(cursor);
        Hovered = hovered;

        if (activated)
        {
            dragging = true;
            pressY = mouse.Y;
            pressPosition = position.Value;
            travel = 0f;
        }

        var result = value;
        if (dragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                travel = MathF.Max(travel, MathF.Abs(mouse.Y - pressY));
                var raw = pressPosition - (mouse.Y - pressY) / rowHeight;
                position.SnapTo(Clamp(raw, count, wrap));
                Tick(position.Value);
            }
            else
            {
                dragging = false;
                var landed = travel <= TapSlopUnits * scale
                    ? MathF.Round(position.Value) + MathF.Round((mouse.Y - column.Center.Y) / rowHeight)
                    : MathF.Round(position.Value);
                result = Normalize((int)Clamp(landed, count, wrap), count);
                position.SnapTo(position.Value);
                SetTarget(landed, count, wrap);
            }
        }
        else if (hovered && ImGui.GetIO().MouseWheel != 0f)
        {
            var step = ImGui.GetIO().MouseWheel > 0f ? -1 : 1;
            var landed = Clamp(MathF.Round(target) + step, count, wrap);
            result = Normalize((int)landed, count);
            SetTarget(landed, count, wrap);
        }
        else if (Normalize((int)MathF.Round(target), count) != Normalize(value, count))
        {
            SetTarget(Nearest(value, position.Value, count, wrap), count, wrap);
        }

        if (!dragging)
        {
            position.Step(target, SnapSeconds, delta);
            Tick(position.Value);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
        }

        Paint(drawList, column, labels, wrap, style, rowHeight);
        return result;
    }

    private void SetTarget(float landed, int count, bool wrap)
    {
        target = Clamp(landed, count, wrap);
    }

    private void Tick(float value)
    {
        var rounded = (int)MathF.Round(value);
        if (rounded == lastTick)
        {
            return;
        }

        lastTick = rounded;
        UiFeedback.Play(UiSound.Keystroke);
    }

    private void Paint(ImDrawListPtr drawList, Rect column, IReadOnlyList<string> labels, bool wrap,
        in WheelStyle style, float rowHeight)
    {
        var count = labels.Count;
        var center = column.Center;
        var anchor = (int)MathF.Floor(position.Value);
        drawList.PushClipRect(column.Min, column.Max, true);
        for (var offset = -DrawReach; offset <= DrawReach + 1; offset++)
        {
            var index = anchor + offset;
            if (!wrap && (index < 0 || index >= count))
            {
                continue;
            }

            var distance = index - position.Value;
            var angle = Math.Clamp(distance * Curvature, -MathF.PI * 0.5f, MathF.PI * 0.5f);
            var y = center.Y + MathF.Sin(angle) / Curvature * rowHeight;
            var fade = 1f - MathF.Pow(Math.Clamp(MathF.Abs(distance) / FadeReach, 0f, 1f), 1.4f);
            if (fade <= 0.01f)
            {
                continue;
            }

            var focus = Math.Clamp(1f - MathF.Abs(distance), 0f, 1f);
            var ink = Vector4.Lerp(style.Muted, style.Ink, focus);
            Typography.DrawCentered(drawList, new Vector2(center.X, y), labels[Normalize(index, count)],
                ink with { W = ink.W * fade }, style.Text);
        }

        drawList.PopClipRect();
    }

    private static float Clamp(float value, int count, bool wrap) => wrap ? value : Math.Clamp(value, 0f, count - 1);

    private static int Normalize(int value, int count) => (value % count + count) % count;

    private static float Nearest(int value, float current, int count, bool wrap)
    {
        if (!wrap)
        {
            return value;
        }

        var turns = MathF.Round((current - value) / count);
        return value + turns * count;
    }
}
