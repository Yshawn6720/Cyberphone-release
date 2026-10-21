using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music.Components;

internal static class SectionHeader
{
    private const float FirstGap = 4f;
    private const float ChevronGap = 6f;
    private const float ChevronBox = 14f;
    private const float ChevronScale = 0.7f;

    public static bool Draw(AppSkin ui, string title, bool tappable, float inset = MusicUi.Inset,
        float topGap = MusicUi.SectionGap)
    {
        var scale = UiScale.Current;
        var firstOnPage = ImGui.GetCursorPosY() <= ImGui.GetStyle().WindowPadding.Y + 1f;
        ImGui.Dummy(new Vector2(0f, (firstOnPage ? MathF.Min(topGap, FirstGap) : topGap) * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X + inset * scale;
        var bandWidth = MathF.Max(1f, width - inset * 2f * scale);
        var bandHeight = CardSectionHeader.HeightUnits * scale;
        var chevronSpace = tappable ? (ChevronGap + ChevronBox) * scale : 0f;
        var fitted = Typography.FitText(title, MathF.Max(1f, bandWidth - chevronSpace), TextStyles.Title3);
        var textWidth = Typography.Measure(fitted, TextStyles.Title3).X;
        var drawList = ImGui.GetWindowDrawList();
        var min = new Vector2(left, origin.Y);
        var max = new Vector2(left + textWidth + chevronSpace, origin.Y + bandHeight);
        var hovered = tappable && UiInteract.Hover(min, max);
        var ink = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? ui.MutedInk : ui.TitleInk;
        CardSectionHeader.Draw(drawList, min, bandWidth, title, ink, chevronSpace);
        if (tappable)
        {
            var chevronCenter = new Vector2(left + textWidth + (ChevronGap + ChevronBox * 0.5f) * scale,
                origin.Y + bandHeight * 0.5f);
            AppSkin.Icon(drawList, chevronCenter, IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk,
                ChevronScale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bandHeight));
        return tappable && UiInteract.Click(min, max, hovered);
    }
}
