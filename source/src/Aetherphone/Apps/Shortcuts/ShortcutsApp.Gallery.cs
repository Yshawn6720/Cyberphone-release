using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const float IntroGap = 6f;

    private void DrawGallery(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var scale = UiScale.Current;
        using (AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y + Typography.DrawWrappedLeft(origin, Loc.T(L.Shortcuts.GalleryIntro), ui.MutedInk,
                TextStyles.Subheadline, width);
            cursorY += IntroGap * scale;
            for (var groupIndex = 0; groupIndex < ShortcutTemplates.Groups.Length; groupIndex++)
            {
                cursorY = DrawTemplateGroup(drawList, new Vector2(origin.X, cursorY), width,
                    ShortcutTemplates.Groups[groupIndex], scale);
            }

            ShortcutsArt.ReserveTo(origin, width, cursorY + ShortcutsArt.BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "shortcuts.nav.gallery", Loc.T(L.Shortcuts.TabGallery),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawTemplateGroup(ImDrawListPtr drawList, Vector2 origin, float width, ShortcutTemplateGroup group,
        float scale)
    {
        var top = origin.Y + ShortcutsArt.SectionGap * scale * 0.5f;
        top += CardSectionHeader.Draw(drawList, new Vector2(origin.X, top), width,
            Loc.T(ShortcutTemplates.GroupTitle(group)), ui.TitleInk) + ShortcutsArt.HeaderGap * scale;
        var bottom = top;
        var slot = 0;
        var templates = ShortcutTemplates.All;
        for (var index = 0; index < templates.Length; index++)
        {
            var template = templates[index];
            if (template.Group != group)
            {
                continue;
            }

            var rect = TileRect(new Vector2(origin.X, top), width, slot, scale);
            slot++;
            bottom = rect.Max.Y;
            if (index == 0)
            {
                UiAnchors.Report("shortcuts.gallery.tile", rect);
            }

            if (ImGui.IsRectVisible(rect.Min, rect.Max) && DrawTemplateTile(drawList, rect, index, template, scale))
            {
                OpenTemplate(template);
            }
        }

        return bottom;
    }

    private bool DrawTemplateTile(ImDrawListPtr drawList, Rect rect, int index, ShortcutTemplate template, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var drawn = Pressed(rect, ImGui.GetID($"##shortcutTemplate{index}"),
            hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var name = Loc.T(template.Name);
        ShortcutsArt.TileBody(drawList, drawn, ShortcutTint.Resolve(template.Tint), hovered ? HoverLift : 0f, scale);
        ShortcutsArt.TileGlyph(drawList, drawn, (int)template.Glyph, name, null, scale);
        ShortcutsArt.TileName(drawList, drawn, name, string.Empty, scale);
        var added = store.FindByName(name) is not null;
        ShortcutsArt.CornerBadge(drawList, ShortcutsArt.CornerCenter(drawn, scale),
            added ? FontAwesomeIcon.Check : FontAwesomeIcon.Plus, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void OpenTemplate(ShortcutTemplate template)
    {
        previewEntry = template.Build();
        Push(ShortcutsRoute.Preview, RootTitle());
    }
}
