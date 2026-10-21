using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health;

internal sealed record StepperIds(string Minus, string Plus)
{
    public static StepperIds For(string id) => new(id + ".minus", id + ".plus");
}

internal sealed partial class HealthApp
{
    private const float RowHeight = 52f;
    private const float StepperRadius = RoundButton.SmallRadius;
    private const float GlyphFraction = 0.9f;
    private const float StepperValueWidth = 92f;

    private float RowsCard(ImDrawListPtr drawList, Vector2 origin, float width, int rows, float scale, out Rect card)
    {
        var height = rows * RowHeight * scale;
        card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        return card.Max.Y;
    }

    private Rect CardRow(ImDrawListPtr drawList, Rect card, int row, float scale)
    {
        var pad = HealthArt.CardPad * scale;
        var top = card.Min.Y + row * RowHeight * scale;
        if (row > 0)
        {
            drawList.AddLine(new Vector2(card.Min.X + pad, top), new Vector2(card.Max.X, top),
                ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
        }

        return new Rect(new Vector2(card.Min.X + pad, top), new Vector2(card.Max.X - pad, top + RowHeight * scale));
    }

    private void RowLabel(ImDrawListPtr drawList, Rect row, string label, float reserve, Vector4 ink)
    {
        var text = Typography.FitText(label, MathF.Max(1f, row.Width - reserve), TextStyles.Body);
        var height = Typography.Measure(text, TextStyles.Body).Y;
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - height * 0.5f), text, ink, TextStyles.Body);
    }

    private void RowValue(ImDrawListPtr drawList, Rect row, string value, Vector4 ink)
    {
        var text = Typography.FitText(value, row.Width * 0.55f, TextStyles.Body);
        var size = Typography.Measure(text, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f), text, ink,
            TextStyles.Body);
    }

    private bool ToggleRow(ImDrawListPtr drawList, Rect row, string id, string label, bool value, float scale) =>
        ToggleRow(drawList, row, id, label, value, ui.TitleInk, scale);

    private bool ToggleRow(ImDrawListPtr drawList, Rect row, string id, string label, bool value, Vector4 ink,
        float scale)
    {
        var size = new Vector2(Metrics.Size.ToggleWidth, Metrics.Size.ToggleHeight) * scale;
        var toggle = new Rect(new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + size.Y * 0.5f));
        RowLabel(drawList, row, label, size.X + HealthArt.TileGap * scale, ink);
        return Toggle.Draw(id, toggle, value, theme);
    }

    private int StepperRow(ImDrawListPtr drawList, Rect row, StepperIds ids, string label, string value, bool canDecrease,
        bool canIncrease, float scale)
    {
        var width = (StepperValueWidth + StepperRadius * 4f) * scale;
        var area = new Rect(new Vector2(row.Max.X - width, row.Min.Y), row.Max);
        RowLabel(drawList, row, label, width + HealthArt.TileGap * scale, ui.TitleInk);
        return Stepper(drawList, area, ids, value, canDecrease, canIncrease, ui.Accent, ui.TitleInk,
            TextStyles.Headline, scale);
    }

    private int Stepper(ImDrawListPtr drawList, Rect area, StepperIds ids, string value, bool canDecrease,
        bool canIncrease, Vector4 tint, Vector4 ink, in TextStyle style, float scale)
    {
        var radius = StepperRadius * scale;
        var minusCenter = new Vector2(area.Min.X + radius, area.Center.Y);
        var plusCenter = new Vector2(area.Max.X - radius, area.Center.Y);
        var delta = 0;
        if (StepperButton(drawList, ids.Minus, minusCenter, radius, FontAwesomeIcon.Minus, tint,
                canDecrease))
        {
            delta = -1;
        }

        if (StepperButton(drawList, ids.Plus, plusCenter, radius, FontAwesomeIcon.Plus, tint,
                canIncrease))
        {
            delta = 1;
        }

        var valueWidth = MathF.Max(1f, plusCenter.X - minusCenter.X - radius * 2f - Metrics.Space.Xs * scale);
        var text = Typography.FitText(value, valueWidth, style);
        Typography.DrawCentered(drawList, new Vector2((minusCenter.X + plusCenter.X) * 0.5f, area.Center.Y), text,
            ink, style);
        if (delta != 0)
        {
            UiFeedback.Play(UiSound.Tap);
        }

        return delta;
    }

    private bool StepperButton(ImDrawListPtr drawList, string id, Vector2 center, float radius, FontAwesomeIcon icon,
        Vector4 tint, bool enabled) =>
        GlyphButton(drawList, id, center, radius, icon, tint, ButtonStyle.Tinted, enabled);

    private bool GlyphButton(ImDrawListPtr drawList, string id, Vector2 center, float radius, FontAwesomeIcon icon,
        Vector4 tint, ButtonStyle style, bool enabled, string tooltip = "", Vector4? glyphInk = null)
    {
        var clicked = RoundButton.Draw(drawList, ImGui.GetID(id), center, radius, ui.Ink.WithAccent(tint), style,
            enabled, false, out var face);
        var grow = face.Face.Width / MathF.Max(radius * 2f, 0.0001f);
        var ink = glyphInk is { } custom ? custom with { W = face.LabelInk.W } : face.LabelInk;
        ProgressRing.CenterIcon(drawList, center, icon, ink, radius * GlyphFraction * grow);
        if (enabled && tooltip.Length > 0)
        {
            var extent = new Vector2(radius, radius);
            HoverTooltip.Show(new Rect(center - extent, center + extent), tooltip, HoverLabelSide.Above);
        }

        return clicked;
    }
}
