using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const float SheetPad = 16f;
    private const float SheetTitleHeight = 36f;
    private const float SheetGap = 10f;
    private const float SheetTileHeight = 96f;
    private const float SheetRowHeight = 60f;
    private const float SheetTileRadius = 18f;
    private const float SheetFillAlpha = 0.07f;
    private const float SheetHoverAlpha = 0.13f;
    private const float SheetMutedAlpha = 0.62f;

    private static readonly ShortcutStepKind[] ActionKinds =
    {
        ShortcutStepKind.Command, ShortcutStepKind.Wait, ShortcutStepKind.OpenPlugin, ShortcutStepKind.OpenUrl,
    };

    private readonly Sheet actionSheet = new();

    private void OpenActionSheet()
    {
        if (draft is null || draft.Steps.Count >= ShortcutStore.MaxSteps)
        {
            UiFeedback.Play(UiSound.Blocked);
            return;
        }

        actionSheet.Open();
    }

    private void DrawActionSheet(Rect screen)
    {
        if (!actionSheet.CapturesPointer)
        {
            return;
        }

        var scale = UiScale.Current;
        var height = (SheetMetrics.GrabberZone + SheetTitleHeight + SheetGap + SheetTileHeight * 2f + SheetGap * 2f +
                      SheetRowHeight + Metrics.Size.HomeIndicatorInset) * scale;
        using var layer = ScreenLayer.Begin("shortcuts.actionSheet", screen, false);
        var frame = actionSheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Fitted(height),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var left = content.Min.X + SheetPad * scale;
        var right = content.Max.X - SheetPad * scale;
        var y = content.Min.Y;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, y + SheetTitleHeight * scale * 0.5f),
            Loc.T(L.Shortcuts.AddAction), ink, TextStyles.Headline);
        y += (SheetTitleHeight + SheetGap) * scale;
        var gap = SheetGap * scale;
        var tileWidth = (right - left - gap) * 0.5f;
        var picked = -1;
        for (var index = 0; index < ActionKinds.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(left + column * (tileWidth + gap), y + row * (SheetTileHeight * scale + gap));
            var rect = new Rect(min, min + new Vector2(tileWidth, SheetTileHeight * scale));
            if (DrawActionTile(drawList, rect, ActionKinds[index], ink, frame.Interactive, scale))
            {
                picked = index;
            }
        }

        y += SheetTileHeight * 2f * scale + gap * 2f;
        var pasteRect = new Rect(new Vector2(left, y), new Vector2(right, y + SheetRowHeight * scale));
        var pasted = DrawPasteRow(drawList, pasteRect, ink, frame.Interactive, scale);
        actionSheet.End(in frame);
        if (picked >= 0)
        {
            AddStep(ActionKinds[picked]);
        }
        else if (pasted)
        {
            PasteMacro();
        }
    }

    private bool DrawActionTile(ImDrawListPtr drawList, Rect rect, ShortcutStepKind kind, Vector4 ink,
        bool interactive, float scale)
    {
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var grow = PressFx.Scale(ImGui.GetID($"##shortcutActionKind{(int)kind}"),
            hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), PressFx.CardPressedScale);
        var drawn = Scaled(rect, grow);
        Squircle.Fill(drawList, drawn.Min, drawn.Max, SheetTileRadius * scale,
            ImGui.GetColorU32(ink with { W = ink.W * (hovered ? SheetHoverAlpha : SheetFillAlpha) }));
        var pad = Metrics.Space.Md * scale;
        var badge = ShortcutsArt.BadgeSize * scale;
        ShortcutsArt.KindBadge(drawList, new Vector2(drawn.Min.X + pad + badge * 0.5f, drawn.Min.Y + pad + badge * 0.5f),
            badge, kind);
        var width = MathF.Max(1f, drawn.Width - pad * 2f);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var bodyHeight = Typography.LineHeight(TextStyles.Footnote);
        var bodyTop = drawn.Max.Y - pad - bodyHeight;
        Typography.Draw(drawList, new Vector2(drawn.Min.X + pad, bodyTop - titleHeight),
            Typography.FitText(KindTitle(kind), width, TextStyles.BodyEmphasized), ink, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(drawn.Min.X + pad, bodyTop),
            Typography.FitText(KindBody(kind), width, TextStyles.Footnote), ink with { W = ink.W * SheetMutedAlpha },
            TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private bool DrawPasteRow(ImDrawListPtr drawList, Rect rect, Vector4 ink, bool interactive, float scale)
    {
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        Squircle.Fill(drawList, rect.Min, rect.Max, SheetTileRadius * scale,
            ImGui.GetColorU32(ink with { W = ink.W * (hovered ? SheetHoverAlpha : SheetFillAlpha) }));
        var pad = Metrics.Space.Md * scale;
        var badge = ShortcutsArt.BadgeSize * scale;
        var badgeMin = new Vector2(rect.Min.X + pad, rect.Center.Y - badge * 0.5f);
        IconTile.FillShaded(drawList, badgeMin, badgeMin + new Vector2(badge), badge * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, badgeMin + new Vector2(badge * 0.5f), FontAwesomeIcon.Paste, AccentRing.Ink,
            badge * 0.46f);
        var textLeft = badgeMin.X + badge + ShortcutsArt.TextGap * scale;
        var width = MathF.Max(1f, rect.Max.X - pad - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var bodyHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = rect.Center.Y - (titleHeight + bodyHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.Shortcuts.PasteMacro), width, TextStyles.BodyEmphasized), ink,
            TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(Loc.T(L.Shortcuts.PasteMacroBody), width, TextStyles.Footnote),
            ink with { W = ink.W * SheetMutedAlpha }, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static string KindBody(ShortcutStepKind kind) => kind switch
    {
        ShortcutStepKind.Wait => Loc.T(L.Shortcuts.ActionWaitBody),
        ShortcutStepKind.OpenPlugin => Loc.T(L.Shortcuts.ActionPluginBody),
        ShortcutStepKind.OpenUrl => Loc.T(L.Shortcuts.ActionLinkBody),
        _ => Loc.T(L.Shortcuts.ActionCommandBody),
    };

    private void AddStep(ShortcutStepKind kind)
    {
        if (draft is null || draft.Steps.Count >= ShortcutStore.MaxSteps)
        {
            actionSheet.Close();
            return;
        }

        if (kind == ShortcutStepKind.OpenPlugin)
        {
            actionSheet.CloseImmediately();
            OpenPluginPicker(PickerPurpose.AddStep, -1);
            return;
        }

        draft.Steps.Add(kind == ShortcutStepKind.Wait
            ? new ShortcutStep { Kind = kind, Seconds = 1f }
            : new ShortcutStep { Kind = kind });
        focusStepIndex = kind == ShortcutStepKind.Wait ? -1 : draft.Steps.Count - 1;
        ResetBlockSprings();
        actionSheet.Close();
    }

    private void PasteMacro()
    {
        actionSheet.Close();
        var added = ShortcutMacro.Append(ImGui.GetClipboardText(), draft!.Steps, ShortcutStore.MaxSteps,
            out var truncated);
        ResetBlockSprings();
        if (truncated)
        {
            UiFeedback.Play(UiSound.Blocked);
            Warn(Loc.T(L.Shortcuts.StepLimitReached, ShortcutStore.MaxSteps));
            return;
        }

        if (added == 0)
        {
            UiFeedback.Play(UiSound.Blocked);
            Warn(Loc.T(L.Shortcuts.PasteEmpty));
        }
    }
}
