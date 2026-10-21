using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Announcements;

internal static class AnnouncementsStatePanel
{
    private const float TileSize = 72f;
    private const float GlyphFraction = 0.46f;
    private const float TileToTitle = 20f;
    private const float TitleToHint = 6f;
    private const float HintToAction = 22f;
    private const float ActionHeight = Button.LargeHeight;
    private const float ActionPadding = 44f;
    private const float ActionMinWidth = 140f;
    private const float MaxTextWidth = 280f;
    private const float SideMargin = 32f;
    private const float VerticalBias = 0.42f;
    private const float TextLineSpacing = 1.25f;
    private const string ActionId = "announcements.state.action";

    public static bool Draw(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint, string action)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var textWidth = MathF.Min(body.Width - SideMargin * 2f * scale, MaxTextWidth * scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Title3, textWidth).Y;
        var hintHeight = hint.Length == 0
            ? 0f
            : TitleToHint * scale + Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, textWidth).Y;
        var actionHeight = action.Length == 0 ? 0f : (HintToAction + ActionHeight) * scale;
        var tile = TileSize * scale;
        var total = tile + TileToTitle * scale + titleHeight + hintHeight + actionHeight;
        var top = body.Min.Y + MathF.Max(0f, (body.Height - total) * VerticalBias);
        var centerX = body.Center.X;

        var tileMin = new Vector2(centerX - tile * 0.5f, top);
        var tileMax = tileMin + new Vector2(tile, tile);
        var radius = tile * Metrics.Radius.TileFactor;
        Elevation.IconRest(drawList, tileMin, tileMax, radius, scale);
        IconTile.FillShaded(drawList, tileMin, tileMax, radius, IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, tile * GlyphFraction);

        var cursorY = tileMax.Y + TileToTitle * scale;
        cursorY = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, cursorY), textWidth, TextLineSpacing);
        if (hint.Length > 0)
        {
            cursorY = Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, cursorY + TitleToHint * scale), textWidth, TextLineSpacing);
        }

        if (action.Length == 0)
        {
            return false;
        }

        var natural = Typography.Measure(action, TextStyles.Headline).X + ActionPadding * scale;
        var width = Math.Clamp(natural, ActionMinWidth * scale, MathF.Max(ActionMinWidth * scale, textWidth));
        var actionTop = cursorY + HintToAction * scale;
        var rest = new Rect(new Vector2(centerX - width * 0.5f, actionTop),
            new Vector2(centerX + width * 0.5f, actionTop + ActionHeight * scale));
        return Button.Draw(drawList, rest, action, ui.Ink, ButtonStyle.Prominent, id: ActionId);
    }
}
