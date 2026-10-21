using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float EarnPad = 12f;
    private const float EarnHintGap = 3f;
    private const float EarnBarGap = 10f;
    private const float EarnBarHeight = 4f;
    private const float EarnBarTrackAlpha = 0.14f;
    private const float EarnCheckSize = 11f;
    private const float EarnCaptionGap = 6f;

    private CachedText[] earnProgress = Array.Empty<CachedText>();

    private float DrawEarn(ImDrawListPtr drawList, Vector2 origin, float width, CoinWalletDto wallet, float scale)
    {
        var rules = wallet.Rules;
        if (rules.Length == 0)
        {
            return origin.Y;
        }

        if (earnProgress.Length < rules.Length)
        {
            earnProgress = new CachedText[rules.Length];
        }

        var headerTop = origin.Y + CoinArt.SectionGap * scale;
        var cursorY = headerTop + CoinArt.SectionHeader(drawList, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Coin.EarnHeader), ui.TitleInk, 0f, scale);
        cursorY = DrawEarnGroup(drawList, new Vector2(origin.X, cursorY), width, rules, false,
            Loc.T(L.Coin.DailyGoals), scale);
        cursorY = DrawEarnGroup(drawList, new Vector2(origin.X, cursorY), width, rules, true,
            Loc.T(L.Coin.WeeklyGoals), scale);
        return cursorY;
    }

    private float DrawEarnGroup(ImDrawListPtr drawList, Vector2 origin, float width, CoinRuleStatusDto[] rules,
        bool weekly, string caption, float scale)
    {
        var total = 0f;
        var count = 0;
        for (var index = 0; index < rules.Length; index++)
        {
            if (rules[index].Weekly != weekly)
            {
                continue;
            }

            total += EarnRowHeight(rules[index], width, scale);
            count++;
        }

        if (count == 0)
        {
            return origin.Y;
        }

        var cursorY = origin.Y + (weekly ? Metrics.Space.Lg * scale : 0f);
        var captionHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(origin.X, cursorY), Loc.Upper(caption), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        cursorY += captionHeight + EarnCaptionGap * scale;
        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + total);
        if (!ImGui.IsRectVisible(min, max))
        {
            return max.Y;
        }

        CoinArt.Card(drawList, ui, min, max, scale);
        var drawn = 0;
        cursorY = DrawEarnRows(drawList, min.X, cursorY, width, rules, weekly, false, ref drawn, scale);
        DrawEarnRows(drawList, min.X, cursorY, width, rules, weekly, true, ref drawn, scale);
        return max.Y;
    }

    private float DrawEarnRows(ImDrawListPtr drawList, float left, float top, float width, CoinRuleStatusDto[] rules,
        bool weekly, bool complete, ref int drawn, float scale)
    {
        var cursorY = top;
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            if (rule.Weekly != weekly || CoinGoals.IsComplete(rule) != complete)
            {
                continue;
            }

            var height = EarnRowHeight(rule, width, scale);
            var row = new Rect(new Vector2(left, cursorY), new Vector2(left + width, cursorY + height));
            if (drawn > 0)
            {
                DrawRowHairline(drawList, row, scale);
            }

            DrawEarnRow(drawList, row, rule, index, scale);
            drawn++;
            cursorY += height;
        }

        return cursorY;
    }

    private float EarnRowHeight(CoinRuleStatusDto rule, float width, float scale)
    {
        var textWidth = EarnTextWidth(width, scale);
        var height = EarnPad * scale * 2f + Typography.LineHeight(TextStyles.BodyEmphasized);
        if (CoinRuleLabels.TryHint(rule.RuleId, out var hint))
        {
            height += EarnHintGap * scale +
                      Typography.MeasureWrappedBlock(Loc.T(hint), TextStyles.Footnote, textWidth).Y;
        }

        if (rule.PeriodCap > 0)
        {
            height += (EarnBarGap + EarnBarHeight) * scale;
        }

        return MathF.Max(height, CoinArt.RowHeight * scale);
    }

    private static float EarnTextWidth(float width, float scale) =>
        MathF.Max(1f, width - Metrics.Space.Lg * scale * 2f - (CoinArt.RowTile + CoinArt.TextGap) * scale);

    private void DrawEarnRow(ImDrawListPtr drawList, Rect row, CoinRuleStatusDto rule, int ruleIndex, float scale)
    {
        var appId = rule.App.Length == 0 ? Id : rule.App;
        var openable = !string.Equals(appId, Id, StringComparison.Ordinal) && navigation.IsAvailable(appId);
        var hovered = openable && CoinArt.RowInteraction(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var tile = CoinArt.RowTile * scale;
        var top = row.Min.Y + EarnPad * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, top + tile * 0.5f);
        IconTile.DrawApp(drawList, appId, tileCenter, tile, IconTile.Surface(AppAccents.For(appId)));

        var complete = CoinGoals.IsComplete(rule);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textRight = row.Max.X - pad;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var progressRight = textRight;
        var progressWidth = 0f;
        if (rule.PeriodCap > 0)
        {
            var progress = EarnProgress(ruleIndex, rule);
            progressWidth = WidgetText.TabularWidth(progress, TextStyles.SubheadlineEmphasized);
            var progressHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
            var progressTop = top + (titleHeight - progressHeight) * 0.5f;
            WidgetText.Tabular(drawList, new Vector2(progressRight - progressWidth, progressTop), progress,
                complete ? ui.Accent : ui.MutedInk, TextStyles.SubheadlineEmphasized);
            if (complete)
            {
                var check = EarnCheckSize * scale;
                progressWidth += check + Metrics.Space.Xs * scale;
                ProgressRing.CenterIcon(drawList,
                    new Vector2(progressRight - progressWidth + check * 0.5f, top + titleHeight * 0.5f),
                    FontAwesomeIcon.CheckCircle, ui.Accent, check);
            }
        }

        var titleWidth = MathF.Max(1f, textRight - progressWidth - CoinArt.ValueGap * scale - textLeft);
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(CoinRuleLabels.For(rule.RuleId)), titleWidth, TextStyles.BodyEmphasized),
            ui.TitleInk, TextStyles.BodyEmphasized);
        var cursorY = top + titleHeight;
        if (CoinRuleLabels.TryHint(rule.RuleId, out var hint))
        {
            cursorY += EarnHintGap * scale;
            cursorY += Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), Loc.T(hint), ui.MutedInk,
                TextStyles.Footnote, textRight - textLeft);
        }

        if (rule.PeriodCap > 0)
        {
            var barTop = cursorY + EarnBarGap * scale;
            var fraction = CoinGoals.Progress(rule);
            CoinArt.Bar(drawList, new Vector2(textLeft, barTop), new Vector2(textRight, barTop + EarnBarHeight * scale),
                fraction, Palette.WithAlpha(ui.TitleInk, EarnBarTrackAlpha), ui.Accent);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            navigation.Open(appId);
        }
    }

    private string EarnProgress(int ruleIndex, CoinRuleStatusDto rule)
    {
        ref var cache = ref earnProgress[ruleIndex];
        var key = (rule.EarnedThisPeriod << 24) ^ rule.PeriodCap;
        return cache.IsCurrent(key)
            ? cache.Value
            : cache.Store(key, Loc.T(L.Coin.EarnProgress, NumberText.Group(rule.EarnedThisPeriod),
                NumberText.Group(rule.PeriodCap)));
    }

    private void DrawRowHairline(ImDrawListPtr drawList, Rect row, float scale) =>
        CoinArt.Hairline(drawList, ui, row.Min.X + (Metrics.Space.Lg + CoinArt.RowTile + CoinArt.TextGap) * scale,
            row.Max.X - Metrics.Space.Lg * scale, row.Min.Y);
}
