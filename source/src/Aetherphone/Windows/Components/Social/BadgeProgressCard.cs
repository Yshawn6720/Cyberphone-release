using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class BadgeProgressCard
{
    private const float PanelRounding = 16f;
    private const float PanelPadX = 14f;
    private const float PanelPadY = 12f;
    private const float TitleGap = 2f;
    private const float HeaderGap = 12f;
    private const float RowGap = 14f;
    private const float GlyphSize = 24f;
    private const float GlyphGap = 10f;
    private const float GoalGap = 6f;
    private const float BarGap = 5f;
    private const float BarHeight = 6f;
    private const float ValueGap = 8f;
    private const float EarnedIconGap = 4f;
    private const float BottomMargin = 12f;
    private const float TrackAlpha = 0.12f;

    private static readonly TextStyle TitleStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle NameStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle HintStyle = TextStyles.Footnote;
    private static readonly TextStyle GoalLabelStyle = TextStyles.Footnote;
    private static readonly TextStyle GoalValueStyle = TextStyles.FootnoteEmphasized;

    public static void Draw(BadgeProgressView? progress, SocialInk ink, bool light, string app)
    {
        if (progress is null || progress.Rows.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var panelLeft = origin.X + SocialChrome.CellPadX * scale;
        var panelRight = origin.X + width - SocialChrome.CellPadX * scale;
        var left = panelLeft + PanelPadX * scale;
        var right = panelRight - PanelPadX * scale;
        var innerWidth = MathF.Max(1f, right - left);

        var hint = Loc.T(L.Social.BadgeProgressHint);
        var titleHeight = Typography.LineHeight(TitleStyle);
        var hintHeight = Typography.MeasureWrappedBlock(hint, HintStyle, innerWidth).Y;
        var nameHeight = MathF.Max(GlyphSize * scale, Typography.LineHeight(NameStyle));
        var goalLabelHeight = Typography.LineHeight(GoalLabelStyle);
        var goalHeight = goalLabelHeight + (BarGap + BarHeight) * scale;
        var openRowHeight = nameHeight + (GoalGap * scale + goalHeight) * 2f;

        var shown = 0;
        var rowsHeight = 0f;
        for (var index = 0; index < progress.Rows.Length; index++)
        {
            var row = progress.Rows[index];
            if (!Shows(row, app))
            {
                continue;
            }

            rowsHeight += (shown > 0 ? RowGap * scale : 0f) + (row.Held ? nameHeight : openRowHeight);
            shown++;
        }

        if (shown == 0)
        {
            return;
        }

        var panelHeight = PanelPadY * scale + titleHeight + TitleGap * scale + hintHeight + HeaderGap * scale
            + rowsHeight + PanelPadY * scale;
        var panelMin = new Vector2(panelLeft, origin.Y);
        var panelMax = new Vector2(panelRight, origin.Y + panelHeight);
        var rounding = PanelRounding * scale;
        Squircle.Fill(drawList, panelMin, panelMax, rounding, ImGui.GetColorU32(ink.ChipFill));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding, ImGui.GetColorU32(ink.ChipStroke),
            Metrics.Stroke.Hairline * scale);

        var cursorY = origin.Y + PanelPadY * scale;
        Typography.Draw(drawList, new Vector2(left, cursorY), Loc.T(L.Social.BadgeProgress), ink.TitleInk, TitleStyle);
        cursorY += titleHeight + TitleGap * scale;
        Typography.DrawWrappedLeft(new Vector2(left, cursorY), hint, ink.MutedInk, HintStyle, innerWidth);
        cursorY += hintHeight + HeaderGap * scale;

        var drawn = 0;
        for (var index = 0; index < progress.Rows.Length; index++)
        {
            var row = progress.Rows[index];
            if (!Shows(row, app))
            {
                continue;
            }

            if (drawn > 0)
            {
                cursorY += RowGap * scale;
            }

            drawn++;
            var badge = UserName.FindBadge(row.BadgeId);
            var accent = badge is null ? ink.Accent : RoleInk.For(badge.Colors[0], light);
            var glyphCenter = new Vector2(left + GlyphSize * scale * 0.5f, cursorY + nameHeight * 0.5f);
            UserName.DrawBadge(drawList, glyphCenter, badge, light, GlyphSize * scale);

            var nameLeft = left + (GlyphSize + GlyphGap) * scale;
            var nameTop = cursorY + (nameHeight - Typography.LineHeight(NameStyle)) * 0.5f;
            if (row.Held)
            {
                var earned = Loc.T(L.Social.BadgeEarned);
                var earnedSize = Typography.Measure(earned, GoalValueStyle);
                var earnedLeft = right - earnedSize.X;
                var iconSize = goalLabelHeight;
                var nameLimit = MathF.Max(1f, earnedLeft - iconSize - (EarnedIconGap + ValueGap) * scale - nameLeft);
                DrawName(drawList, badge, nameLeft, nameTop, nameLimit, ink);
                Typography.Draw(drawList, new Vector2(earnedLeft, cursorY + (nameHeight - earnedSize.Y) * 0.5f), earned,
                    accent, GoalValueStyle);
                PhoneIcon.Draw(drawList,
                    new Vector2(earnedLeft - EarnedIconGap * scale - iconSize * 0.5f, cursorY + nameHeight * 0.5f),
                    PhoneIcons.CircleCheck, accent, iconSize);
                cursorY += nameHeight;
            }
            else
            {
                DrawName(drawList, badge, nameLeft, nameTop, MathF.Max(1f, right - nameLeft), ink);
                cursorY += nameHeight + GoalGap * scale;
                cursorY = DrawGoal(drawList, left, right, cursorY, Loc.T(L.Social.BadgeGoalFollowers),
                    row.FollowersText, row.FollowersFraction, accent, ink, scale, goalLabelHeight);
                cursorY += GoalGap * scale;
                cursorY = DrawGoal(drawList, left, right, cursorY, Loc.T(L.Social.StatLikes), row.LikesText,
                    row.LikesFraction, accent, ink, scale, goalLabelHeight);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, panelHeight + BottomMargin * scale));
    }

    private static bool Shows(in BadgeProgressRow row, string app)
    {
        return row.Key == app || row.Key == BadgeProgressView.AccountKey;
    }

    private static void DrawName(ImDrawListPtr drawList, BadgeStyle? badge, float left, float top, float limit,
        SocialInk ink)
    {
        if (badge is null)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(badge.Name, limit, NameStyle), ink.TitleInk,
            NameStyle);
    }

    private static float DrawGoal(ImDrawListPtr drawList, float left, float right, float top, string label,
        string value, float fraction, Vector4 accent, SocialInk ink, float scale, float labelHeight)
    {
        var valueSize = Typography.Measure(value, GoalValueStyle);
        var labelLimit = MathF.Max(1f, right - left - valueSize.X - ValueGap * scale);
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(label, labelLimit, GoalLabelStyle),
            ink.MutedInk, GoalLabelStyle);
        Typography.Draw(drawList, new Vector2(right - valueSize.X, top), value, ink.BodyInk, GoalValueStyle);

        var barHeight = BarHeight * scale;
        var barTop = top + labelHeight + BarGap * scale;
        var trackMin = new Vector2(left, barTop);
        var trackMax = new Vector2(right, barTop + barHeight);
        var radius = barHeight * 0.5f;
        Squircle.Fill(drawList, trackMin, trackMax, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ink.TitleInk, TrackAlpha)));
        if (fraction > 0f)
        {
            var fillRight = MathF.Max(trackMin.X + barHeight, trackMin.X + (trackMax.X - trackMin.X) * fraction);
            Squircle.Fill(drawList, trackMin, new Vector2(fillRight, trackMax.Y), radius, ImGui.GetColorU32(accent));
        }

        return trackMax.Y;
    }
}
