using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Activity;

internal sealed partial class ActivityApp
{
    private const float GoalRingRadius = 30f;
    private const float GoalRingThickness = 9f;
    private const float GoalControlTop = 18f;
    private const float GoalControlHeight = 56f;
    private const float GoalButtonRadius = RoundButton.RegularRadius;
    private const float GoalButtonGlyph = 0.9f;
    private const float GoalHeaderGap = 14f;

    private readonly Spring[] goalFills = new Spring[ActivityGoalSteps.GoalCount];
    private readonly CachedText[] goalValues = new CachedText[ActivityGoalSteps.GoalCount];
    private readonly CachedText[] goalPercents = new CachedText[ActivityGoalSteps.GoalCount];

    private static readonly int[] GoalOrder = { 0, ActivityGoalSteps.EndgameGoal, 1, 2 };
    private static readonly string[] GoalMinusIds = { "character.goal.minus.0", "character.goal.minus.1", "character.goal.minus.2", "character.goal.minus.3" };
    private static readonly string[] GoalPlusIds = { "character.goal.plus.0", "character.goal.plus.1", "character.goal.plus.2", "character.goal.plus.3" };

    private static readonly LocString[] GoalNames =
    {
        L.Character.GoalLevels,
        L.Character.RingAdventure,
        L.Character.RingFortune,
        L.Character.GoalEndgame,
    };

    private void DrawGoals(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            for (var orderIndex = 0; orderIndex < GoalOrder.Length; orderIndex++)
            {
                cursorY = DrawGoalCard(drawList, new Vector2(origin.X, cursorY), width, GoalOrder[orderIndex], scale);
                cursorY += ActivityArt.TileGap * scale;
            }

            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Character.GoalsHint),
                ui.MutedInk, TextStyles.Footnote, width);
            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Character.EndgameHint),
                ui.MutedInk, TextStyles.Footnote, width);
            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "character.goals.nav", Loc.T(L.Character.GoalsSection),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawGoalCard(ImDrawListPtr drawList, Vector2 origin, float width, int goal, float scale)
    {
        var pad = ActivityArt.CardPad * scale;
        var ringRadius = GoalRingRadius * scale;
        var height = pad * 2f + ringRadius * 2f + GoalControlTop * scale + GoalControlHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var ring = ActivityGoalSteps.RingOf(goal);
        var tint = ActivityArt.Tint(ring);
        var fraction = GoalFraction(goal, tracker.Today);
        var ringCenter = new Vector2(origin.X + pad + ringRadius, origin.Y + pad + ringRadius);
        var shown = goalFills[goal].Step(fraction, Motion.Sheet, ActivityArt.FrameDelta());
        ActivityArt.Ring(drawList, ringCenter, ringRadius - GoalRingThickness * scale * 0.5f,
            GoalRingThickness * scale, shown, tint, true);
        ProgressRing.CenterIcon(drawList, ringCenter, ActivityArt.RingIcon(ring), tint, ringRadius * 0.5f);

        var textLeft = ringCenter.X + ringRadius + GoalHeaderGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var percentHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = ringCenter.Y - (nameHeight + percentHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(GoalNames[goal]), textWidth, TextStyles.Headline), tint, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(GoalPercent(goal, fraction), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);

        var index = ActivityGoalSteps.IndexOf(goal, targets);
        var count = ActivityGoalSteps.Count(goal);
        var controlTop = origin.Y + pad + ringRadius * 2f + GoalControlTop * scale;
        var controlCenterY = controlTop + GoalControlHeight * scale * 0.5f;
        var buttonRadius = GoalButtonRadius * scale;
        var minusCenter = new Vector2(origin.X + pad + buttonRadius, controlCenterY);
        var plusCenter = new Vector2(max.X - pad - buttonRadius, controlCenterY);
        var delta = 0;
        if (GoalButton(minusCenter, buttonRadius, FontAwesomeIcon.Minus, tint, index > 0, GoalMinusIds[goal]))
        {
            delta = -1;
        }

        if (GoalButton(plusCenter, buttonRadius, FontAwesomeIcon.Plus, tint, index < count - 1, GoalPlusIds[goal]))
        {
            delta = 1;
        }

        var valueWidth = MathF.Max(1f, plusCenter.X - minusCenter.X - buttonRadius * 2f - pad);
        var value = Typography.FitText(GoalValue(goal), valueWidth, TextStyles.WidgetDisplayCompact);
        var valueSize = Typography.Measure(value, TextStyles.WidgetDisplayCompact);
        var unit = Typography.FitText(digest.Units[goal], valueWidth, TextStyles.FootnoteEmphasized);
        var unitSize = Typography.Measure(unit, TextStyles.FootnoteEmphasized);
        var blockTop = controlCenterY - (valueSize.Y + unitSize.Y) * 0.5f;
        var centerX = (minusCenter.X + plusCenter.X) * 0.5f;
        Typography.Draw(drawList, new Vector2(centerX - valueSize.X * 0.5f, blockTop), value, ui.TitleInk,
            TextStyles.WidgetDisplayCompact);
        Typography.Draw(drawList, new Vector2(centerX - unitSize.X * 0.5f, blockTop + valueSize.Y), unit, tint,
            TextStyles.FootnoteEmphasized);
        if (delta != 0)
        {
            targets = ActivityGoalSteps.With(goal, index + delta, targets);
            ActivityGoalSteps.Apply(configuration, targets);
            configuration.Save();
            UiFeedback.Play(UiSound.Tap);
        }

        return max.Y;
    }

    private bool GoalButton(Vector2 center, float radius, FontAwesomeIcon icon, Vector4 tint, bool enabled,
        string id)
    {
        var drawList = ImGui.GetWindowDrawList();
        var clicked = RoundButton.Draw(drawList, ImGui.GetID(id), center, radius, ui.Ink.WithAccent(tint),
            ButtonStyle.Tinted, enabled, false, out var face);
        var grow = face.Face.Width / MathF.Max(radius * 2f, 0.0001f);
        ProgressRing.CenterIcon(drawList, center, icon, face.LabelInk, radius * GoalButtonGlyph * grow);
        return clicked;
    }

    private float GoalFraction(int goal, ActivityDay today) => goal switch
    {
        0 => ActivityGoals.LevelFraction(targets, today),
        ActivityGoalSteps.EndgameGoal => ActivityGoals.EndgameFraction(targets, today),
        _ => ActivityGoals.Fraction(targets, today, goal),
    };

    private string GoalValue(int goal)
    {
        var key = goal switch
        {
            0 => (long)MathF.Round(targets.Levels * 10f),
            1 => targets.Duties,
            2 => targets.Gil,
            _ => targets.Endgame,
        };
        if (goalValues[goal].IsCurrent(key))
        {
            return goalValues[goal].Value;
        }

        return goalValues[goal].Store(key, goal switch
        {
            0 => ActivityDigest.Levels(targets.Levels),
            1 => ActivityDigest.Number(targets.Duties),
            2 => ActivityDigest.Number(targets.Gil),
            _ => ActivityDigest.Number(targets.Endgame),
        });
    }

    private string GoalPercent(int goal, float fraction)
    {
        var percent = ActivityDigest.PercentValue(fraction);
        if (goalPercents[goal].IsCurrent(percent))
        {
            return goalPercents[goal].Value;
        }

        return goalPercents[goal].Store(percent, Loc.T(L.Character.TodayPercent, percent));
    }
}
