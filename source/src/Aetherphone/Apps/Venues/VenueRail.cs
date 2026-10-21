using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal static class VenueSwipe
{
    // A real ImGui item has to own the press: without an active item Dear ImGui starts its native window drag
    // on any press over the window background, which moved the whole phone while a carousel was dragged.
    public static bool Claim(string id, Rect zone)
    {
        if (zone.Width <= 0f || zone.Height <= 0f)
        {
            return false;
        }

        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(zone.Min);
        ImGui.InvisibleButton(id, new Vector2(zone.Width, zone.Height));
        var activated = ImGui.IsItemActivated() && UiInteract.Hover(zone.Min, zone.Max);
        ImGui.SetCursorScreenPos(cursor);
        return activated;
    }
}

internal sealed class VenueRail
{
    private const float ArrowRadius = 14f;
    private const float PageFraction = 0.8f;

    private readonly KineticScroller scroller = new();
    private bool pressed;
    private bool paging;
    private float pageTarget;
    private Spring pageSpring;

    public float Offset => scroller.Offset;
    public bool Interactive => !scroller.IsDragging;
    public bool Owning => pressed || scroller.IsDragging;

    public void Reset()
    {
        scroller.Reset();
        paging = false;
        pressed = false;
    }

    public void Begin(string id, Rect row, float contentWidth)
    {
        scroller.Scale = UiScale.Current;
        scroller.SetBounds(MathF.Max(0f, contentWidth - row.Width));
        var activated = VenueSwipe.Claim(id, row);
        HandleDrag(activated);
        StepPaging();
    }

    public void DrawArrows(ImDrawListPtr drawList, Rect row, float contentWidth, float inset)
    {
        var maxOffset = MathF.Max(0f, contentWidth - row.Width);
        var offset = scroller.Offset;
        var page = row.Width * PageFraction;
        if (offset > 0.5f && DrawArrow(drawList, new Vector2(row.Min.X + inset, row.Center.Y), PhoneIcons.ChevronLeft))
        {
            PageTo(offset - page, maxOffset);
        }

        if (offset < maxOffset - 0.5f &&
            DrawArrow(drawList, new Vector2(row.Max.X - inset, row.Center.Y), PhoneIcons.ChevronRight))
        {
            PageTo(offset + page, maxOffset);
        }
    }

    private bool DrawArrow(ImDrawListPtr drawList, Vector2 center, string glyph)
    {
        var scale = UiScale.Current;
        var radius = ArrowRadius * scale;
        var extent = new Vector2(radius, radius);
        var hovered = !scroller.IsDragging && UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? MediaOverlay.HoverFill : MediaOverlay.Fill), 28);
        PhoneIcon.Draw(drawList, center, glyph, MediaOverlay.White, 16f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    private void PageTo(float target, float maxOffset)
    {
        pageSpring = new Spring(scroller.Offset);
        pageTarget = Math.Clamp(target, 0f, maxOffset);
        paging = true;
    }

    private void StepPaging()
    {
        if (!paging)
        {
            return;
        }

        if (scroller.IsDragging)
        {
            paging = false;
            return;
        }

        var offset = pageSpring.Step(pageTarget, Motion.PageSettle,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (pageSpring.IsResting(pageTarget, 0.5f, 1f))
        {
            offset = pageTarget;
            paging = false;
        }

        scroller.SyncOffset(offset);
    }

    private void HandleDrag(bool activated)
    {
        var io = ImGui.GetIO();
        var deltaSeconds = io.DeltaTime;
        var mouseX = io.MousePos.X;
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var shouldBlock = false;
        if (pressed)
        {
            if (down)
            {
                var wasDragging = scroller.IsDragging;
                scroller.Move(mouseX, deltaSeconds);
                if (!wasDragging && scroller.IsDragging)
                {
                    UiInteract.CancelPendingTap();
                }

                shouldBlock = scroller.IsDragging;
            }
            else
            {
                shouldBlock = scroller.IsDragging;
                scroller.Release();
                pressed = false;
                scroller.Tick(deltaSeconds);
            }
        }
        else if (activated && down && !UiInteract.InputBlocked)
        {
            scroller.Press(mouseX);
            pressed = true;
        }
        else if (!paging)
        {
            scroller.Tick(deltaSeconds);
        }

        if (shouldBlock)
        {
            UiInteract.BlockThisFrame();
        }
    }
}

internal sealed class VenuePager
{
    private const float SwipeSlop = 10f;

    private readonly Pager pager = new();
    private bool pressed;
    private Vector2 pressPosition;

    public float Value => pager.Value;
    public int Page => pager.Page;
    public bool Dragging => pager.Dragging;
    public bool Owning => pressed || pager.Dragging;

    public void Reset()
    {
        pager.SnapTo(0, 1);
        pressed = false;
    }

    public void Step(float delta, int count) => pager.Step(delta, count);

    public void AnimateTo(int page, int count) => pager.AnimateTo(page, count);

    public void Drive(string id, Rect row, float stride, int count, float delta, float scale)
    {
        var activated = VenueSwipe.Claim(id, row);
        var mouse = ImGui.GetMousePos();
        if (count > 1 && !pressed && activated && !UiInteract.InputBlocked)
        {
            pressed = true;
            pressPosition = mouse;
        }

        if (!pressed)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (pager.Dragging)
            {
                pager.Release(stride, count);
                UiInteract.BlockThisFrame();
            }

            pressed = false;
            return;
        }

        var move = mouse - pressPosition;
        if (!pager.Dragging && MathF.Abs(move.X) > SwipeSlop * scale && MathF.Abs(move.X) > MathF.Abs(move.Y))
        {
            pager.Begin(pressPosition.X);
            UiInteract.CancelPendingTap();
        }

        if (!pager.Dragging)
        {
            return;
        }

        pager.Drag(mouse.X, stride, count, delta);
        UiInteract.BlockThisFrame();
    }
}
