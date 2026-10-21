using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct TranslationFooterLayout
{
    public readonly string Label;
    public readonly string Action;
    public readonly float LabelWidth;
    public readonly float ActionWidth;
    public readonly float Width;
    public readonly float Height;

    public TranslationFooterLayout(string label, string action, float labelWidth, float actionWidth, float width,
        float height)
    {
        Label = label;
        Action = action;
        LabelWidth = labelWidth;
        ActionWidth = actionWidth;
        Width = width;
        Height = height;
    }
}

internal static class TranslationFooter
{
    private const float FontScale = 0.72f;
    private const float Separator = 6f;
    private const float TopGap = 3f;

    private static readonly TextStyle LabelStyle = new(FontScale, FontWeight.Regular);
    private static readonly TextStyle ActionStyle = new(FontScale, FontWeight.Medium);

    public static TranslationFooterLayout Measure(TranslationEntry? entry, float maxWidth, float scale)
    {
        if (entry is null || entry.State == TranslationState.Idle)
        {
            return default;
        }

        TranslationLabels.Resolve(entry, out var label, out var action);
        if (label.Length == 0 && action.Length == 0)
        {
            return default;
        }

        var labelWidth = label.Length > 0 ? Typography.Measure(label, LabelStyle).X : 0f;
        var actionWidth = action.Length > 0 ? Typography.Measure(action, ActionStyle).X : 0f;
        var separator = labelWidth > 0f && actionWidth > 0f ? Separator * scale : 0f;
        var width = MathF.Min(maxWidth, labelWidth + separator + actionWidth);
        return new TranslationFooterLayout(label, action, labelWidth, actionWidth, width,
            Typography.LineHeight(LabelStyle) + TopGap * scale);
    }

    public static bool Draw(ImDrawListPtr drawList, in TranslationFooterLayout layout, Vector2 position,
        Vector4 labelInk, Vector4 actionInk, float alpha, float scale)
    {
        var cursorX = position.X;
        var top = position.Y + TopGap * scale;
        if (layout.Label.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(cursorX, top), layout.Label,
                Palette.WithAlpha(labelInk, labelInk.W * alpha), LabelStyle);
            cursorX += layout.LabelWidth + Separator * scale;
        }

        if (layout.Action.Length == 0)
        {
            return false;
        }

        var actionMin = new Vector2(cursorX, top);
        var actionMax = new Vector2(cursorX + layout.ActionWidth, top + Typography.LineHeight(ActionStyle));
        var hovered = UiInteract.Hover(actionMin, actionMax);
        var ink = hovered ? Palette.Lighten(actionInk, 0.15f) : actionInk;
        Typography.Draw(drawList, actionMin, layout.Action, Palette.WithAlpha(ink, ink.W * alpha), ActionStyle);
        return UiInteract.Click(actionMin, actionMax, hovered);
    }
}
