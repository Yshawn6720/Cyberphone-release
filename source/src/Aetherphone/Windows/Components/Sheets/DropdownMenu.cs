using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal sealed class DropdownMenu
{
    public readonly record struct Item(string Label, string Glyph = "", bool Danger = false, bool Selected = false,
        bool CanEdit = false, bool CanDelete = false);

    public enum RowAction
    {
        Select,
        Edit,
        Delete,
    }

    private const float RowHeight = 36f;
    private const float HeaderHeight = 26f;
    private const float MinWidth = 168f;
    private const float ActionSlotWidth = 24f;
    private const float ActionIconRadius = 11f;
    private const float ScreenMargin = 8f;
    private const float ScrollThumbWidth = 3f;
    private const float ScrollThumbInset = 3f;
    private const float ScrollThumbMinHeight = 24f;
    private const float ScrollThumbAlpha = 0.22f;
    private string ownerId = string.Empty;
    private bool open;
    private Rect anchor;
    private Spring revealSpring;
    private int openedFrame;
    private float scrollOffset;
    private bool revealSelected;

    public bool Open => open;

    public bool IsOpenFor(string id) => open && ownerId == id;

    public void Toggle(string id, Rect anchorRect)
    {
        if (open && ownerId == id)
        {
            Close();
            return;
        }

        ownerId = id;
        anchor = anchorRect;
        open = true;
        revealSpring.SnapTo(0f);
        openedFrame = ImGui.GetFrameCount();
        scrollOffset = 0f;
        revealSelected = true;
    }

    public void Close()
    {
        open = false;
        ownerId = string.Empty;
    }

    public void Gate()
    {
        if (open)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public int Draw(Rect screen, PhoneTheme theme, ReadOnlySpan<Item> items) => Draw(screen, theme, items, out _);

    public string Header { get; set; } = string.Empty;

    public bool KeepOpen { get; set; }

    public bool Detached { get; set; }

    public int Draw(Rect screen, PhoneTheme theme, ReadOnlySpan<Item> items, out RowAction action)
    {
        action = RowAction.Select;
        if (!open || items.Length == 0)
        {
            return -1;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetForegroundDrawList();
        var revealDelta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var reveal = Math.Clamp(revealSpring.Step(1f, Motion.Appear, revealDelta), 0f, 1f);
        var alpha = Math.Clamp(reveal / 0.7f, 0f, 1f);
        var padX = 14f * scale;
        var padY = 6f * scale;
        var rowHeight = RowHeight * scale;
        var glyphReserve = 26f * scale;
        var checkReserve = 22f * scale;
        var actionSlot = ActionSlotWidth * scale;
        var width = MinWidth * scale;
        var anyGlyph = false;
        var anySelected = false;
        var anyEdit = false;
        for (var index = 0; index < items.Length; index++)
        {
            var textWidth = Typography.Measure(items[index].Label, 0.9f, FontWeight.Medium).X;
            anyGlyph |= items[index].Glyph.Length > 0;
            anySelected |= items[index].Selected;
            anyEdit |= items[index].CanEdit;
            width = MathF.Max(width, textWidth + padX * 2f);
        }

        if (anyGlyph)
        {
            width += glyphReserve;
        }

        if (anySelected)
        {
            width += checkReserve;
        }

        if (anyEdit)
        {
            width += actionSlot;
        }

        var headerHeight = 0f;
        if (Header.Length > 0)
        {
            headerHeight = HeaderHeight * scale;
            width = MathF.Max(width, Typography.Measure(Header, TextStyles.Footnote).X + padX * 2f);
        }

        var margin = ScreenMargin * scale;
        var rowsHeight = items.Length * rowHeight;
        var fullHeight = rowsHeight + padY * 2f + headerHeight;
        var height = MathF.Min(fullHeight, MathF.Max(rowHeight + padY * 2f + headerHeight, screen.Height - margin * 2f));
        var maxScroll = fullHeight - height;
        var left = anchor.Min.X;
        if (left + width > screen.Max.X - margin)
        {
            left = anchor.Max.X - width;
        }

        left = Math.Clamp(left, screen.Min.X + margin, MathF.Max(screen.Min.X + margin, screen.Max.X - margin - width));
        var top = anchor.Max.Y + 4f * scale;
        if (top + height > screen.Max.Y - margin)
        {
            top = anchor.Min.Y - 4f * scale - height;
        }

        var topLimit = screen.Min.Y + margin;
        top = Math.Clamp(top, topLimit, MathF.Max(topLimit, screen.Max.Y - margin - height));
        scrollOffset = Scroll(items, rowHeight, height - padY * 2f - headerHeight, maxScroll, new Vector2(left, top),
            new Vector2(left + width, top + height));
        var pivot = new Vector2(Math.Clamp(anchor.Center.X, left, left + width), top < anchor.Min.Y ? top + height : top);
        var revealScale = 0.94f + 0.06f * reveal;
        var min = pivot + (new Vector2(left, top) - pivot) * revealScale;
        var max = pivot + (new Vector2(left + width, top + height) - pivot) * revealScale;
        PopoverSurface.Draw(drawList, min, max, 14f * scale, theme, scale, alpha);
        if (Detached && ImGui.IsMouseHoveringRect(min, max, false))
        {
            ImGui.SetNextFrameWantCaptureMouse(true);
        }

        var clicked = -1;
        var clickedAction = RowAction.Select;
        var headerOffset = headerHeight * revealScale;
        if (Header.Length > 0)
        {
            var headerCenter = new Vector2((min.X + max.X) * 0.5f, min.Y + padY * revealScale + headerOffset * 0.5f);
            Typography.DrawCentered(drawList, headerCenter, Typography.FitText(Header, max.X - min.X - padX, TextStyles.Footnote),
                Palette.WithAlpha(theme.TextMuted, alpha), TextStyles.Footnote);
            var ruleY = min.Y + padY * revealScale + headerOffset - 1f * scale;
            drawList.AddLine(new Vector2(min.X + padY, ruleY), new Vector2(max.X - padY, ruleY),
                ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, alpha)), 1f);
        }

        var viewportMin = new Vector2(min.X, min.Y + padY * revealScale + headerOffset);
        var viewportMax = new Vector2(max.X, max.Y - padY * revealScale);
        drawList.PushClipRect(viewportMin, viewportMax, true);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var rowMin = new Vector2(min.X + padY,
                viewportMin.Y + (index * rowHeight - scrollOffset) * revealScale);
            var rowMax = new Vector2(max.X - padY, rowMin.Y + rowHeight * revealScale);
            if (rowMax.Y <= viewportMin.Y || rowMin.Y >= viewportMax.Y)
            {
                continue;
            }

            var centerY = (rowMin.Y + rowMax.Y) * 0.5f;

            var cursorRight = rowMax.X - 10f * scale;
            if (anySelected)
            {
                cursorRight -= checkReserve;
            }

            Rect? editRect = null;
            if (anyEdit)
            {
                var center = new Vector2(cursorRight - actionSlot * 0.5f, centerY);
                editRect = new Rect(center - new Vector2(ActionIconRadius * scale), center + new Vector2(ActionIconRadius * scale));
                cursorRight -= actionSlot;
            }

            var rowHovered = Hovering(rowMin, rowMax, true) && Hovering(viewportMin, viewportMax, false);
            var editHovered = item.CanEdit && editRect is { } er && Hovering(er.Min, er.Max, true);
            if (rowHovered)
            {
                Squircle.Fill(drawList, rowMin, rowMax, 9f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.07f * alpha)));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (item.CanDelete && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    clicked = index;
                    clickedAction = RowAction.Delete;
                }
                else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    clicked = index;
                    clickedAction = editHovered ? RowAction.Edit : RowAction.Select;
                }
            }

            var ink = item.Danger ? theme.Danger : item.Selected ? theme.Accent : theme.TextStrong;
            var textLeft = rowMin.X + padX - padY;
            if (anyGlyph)
            {
                if (item.Glyph.Length > 0)
                {
                    AppSkin.Icon(drawList, new Vector2(textLeft + 8f * scale, centerY), item.Glyph,
                        Palette.WithAlpha(ink, ink.W * alpha), 0.88f);
                }

                textLeft += glyphReserve;
            }

            var textSize = Typography.Measure(item.Label, 0.9f, FontWeight.Medium);
            Typography.Draw(drawList, new Vector2(textLeft, centerY - textSize.Y * 0.5f), item.Label,
                Palette.WithAlpha(ink, ink.W * alpha), 0.9f, FontWeight.Medium);
            if (item.Selected)
            {
                DrawCheck(drawList, new Vector2(rowMax.X - 16f * scale, centerY), theme.Accent, alpha, scale);
            }

            if (item.CanEdit && editRect is { } editIconRect)
            {
                var tint = editHovered ? theme.Accent : Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * alpha);
                AppSkin.Icon(drawList, editIconRect.Center, IconGlyph.Of(FontAwesomeIcon.Pen), tint, 0.7f);
            }
        }

        drawList.PopClipRect();
        if (maxScroll > 0f)
        {
            DrawScrollThumb(drawList, viewportMin, viewportMax, maxScroll, theme, alpha, scale);
        }

        if (clicked >= 0)
        {
            action = clickedAction;
            if (!KeepOpen)
            {
                Close();
            }

            return clicked;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !Hovering(min, max, false) &&
            ImGui.GetFrameCount() != openedFrame)
        {
            Close();
        }

        return -1;
    }

    private float Scroll(ReadOnlySpan<Item> items, float rowHeight, float viewportHeight, float maxScroll,
        Vector2 min, Vector2 max)
    {
        if (maxScroll <= 0f)
        {
            revealSelected = false;
            return 0f;
        }

        var offset = scrollOffset;
        if (revealSelected)
        {
            revealSelected = false;
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index].Selected)
                {
                    offset = (index + 0.5f) * rowHeight - viewportHeight * 0.5f;
                    break;
                }
            }
        }

        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel != 0f && Hovering(min, max, false))
        {
            offset -= wheel * rowHeight * 2f;
        }

        return Math.Clamp(offset, 0f, maxScroll);
    }

    private void DrawScrollThumb(ImDrawListPtr drawList, Vector2 viewportMin, Vector2 viewportMax, float maxScroll,
        PhoneTheme theme, float alpha, float scale)
    {
        var track = viewportMax.Y - viewportMin.Y;
        var thumbHeight = MathF.Max(ScrollThumbMinHeight * scale, track * track / (track + maxScroll));
        var thumbTop = viewportMin.Y + (track - thumbHeight) * (scrollOffset / maxScroll);
        var right = viewportMax.X - ScrollThumbInset * scale;
        var thumbMin = new Vector2(right - ScrollThumbWidth * scale, thumbTop);
        var thumbMax = new Vector2(right, thumbTop + thumbHeight);
        drawList.AddRectFilled(thumbMin, thumbMax,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, ScrollThumbAlpha * alpha)), ScrollThumbWidth * scale);
    }

    private bool Hovering(Vector2 min, Vector2 max, bool clip) =>
        Detached ? ImGui.IsMouseHoveringRect(min, max, false) : UiInteract.HoverWindowOnly(min, max, clip);

    private static void DrawCheck(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float alpha, float scale)
    {
        var color = ImGui.GetColorU32(Palette.WithAlpha(accent, accent.W * alpha));
        var thickness = 1.8f * scale;
        drawList.AddLine(center + new Vector2(-4f * scale, 0f), center + new Vector2(-1.2f * scale, 3.2f * scale),
            color, thickness);
        drawList.AddLine(center + new Vector2(-1.2f * scale, 3.2f * scale),
            center + new Vector2(4.4f * scale, -3.6f * scale), color, thickness);
    }
}
