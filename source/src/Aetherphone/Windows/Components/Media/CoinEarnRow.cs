using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class CoinEarnRow
{
    private const float Rounding = 16f;
    private const float Inset = 14f;
    private const float TileSize = 32f;
    private const float BarHeight = 3f;
    private const float RowGap = 8f;

    public static void Draw(CoinRuleStatusDto rule, in AppPalette palette)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = Inset * scale;
        var tileSize = TileSize * scale;
        var textLeft = origin.X + inset + tileSize + 12f * scale;
        var textRight = origin.X + width - inset;

        var complete = CoinGoals.IsComplete(rule);
        var hasBar = rule.PeriodCap > 0;
        var title = Loc.T(CoinRuleLabels.For(rule.RuleId));
        var hasHint = CoinRuleLabels.TryHint(rule.RuleId, out var hintEntry);
        var hint = hasHint ? Loc.T(hintEntry) : string.Empty;
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var hintBlock = hasHint
            ? Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textRight - textLeft)
            : Vector2.Zero;
        var height = 12f * scale + titleSize.Y
            + (hasHint ? 4f * scale + hintBlock.Y : 0f)
            + (hasBar ? (12f + BarHeight) * scale : 0f)
            + 12f * scale;

        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = Rounding * scale;
        var accent = palette.Accent;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(palette.CardFill));
        if (complete)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.08f)));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.45f)),
                1f * scale);
        }

        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var titleTop = min.Y + 12f * scale;
        var appId = rule.App.Length == 0 ? "coin" : rule.App;
        var tileCenter = new Vector2(min.X + inset + tileSize * 0.5f, (min.Y + max.Y) * 0.5f);
        IconTile.DrawApp(drawList, appId, tileCenter, tileSize, IconTile.Surface(AppAccents.For(appId)));

        var progress = NumberText.Group(rule.EarnedThisPeriod) + " / "
            + NumberText.Group(rule.PeriodCap);
        var progressSize = Typography.Measure(progress, TextStyles.SubheadlineEmphasized);
        var checkSize = complete ? 11f * scale : 0f;
        var checkPad = complete ? 6f * scale : 0f;
        var progressLeft = textRight - progressSize.X;
        var titleWidth = MathF.Max(40f * scale, progressLeft - checkSize - checkPad - 10f * scale - textLeft);
        Typography.Draw(drawList, new Vector2(textLeft, titleTop),
            Typography.FitText(title, titleWidth, TextStyles.SubheadlineEmphasized), palette.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(progressLeft, titleTop), progress,
            complete ? accent : palette.MutedInk, TextStyles.SubheadlineEmphasized);
        if (complete)
        {
            ProgressRing.CenterIcon(drawList,
                new Vector2(progressLeft - checkPad - checkSize * 0.5f, titleTop + progressSize.Y * 0.5f),
                FontAwesomeIcon.CheckCircle, accent, checkSize);
        }

        if (hasHint)
        {
            Typography.DrawWrappedLeft(new Vector2(textLeft, titleTop + titleSize.Y + 4f * scale), hint,
                palette.MutedInk, TextStyles.Footnote, textRight - textLeft);
        }

        if (hasBar)
        {
            var barHeight = BarHeight * scale;
            var barMin = new Vector2(textLeft, max.Y - 12f * scale - barHeight);
            var barMax = new Vector2(textRight, barMin.Y + barHeight);
            drawList.AddRectFilled(barMin, barMax,
                ImGui.GetColorU32(Palette.WithAlpha(palette.MutedInk, 0.18f)), barHeight * 0.5f);
            var fraction = Math.Clamp(rule.EarnedThisPeriod / (float)rule.PeriodCap, 0f, 1f);
            if (fraction > 0f)
            {
                drawList.AddRectFilled(barMin, new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y),
                    ImGui.GetColorU32(complete ? accent : Palette.WithAlpha(accent, 0.85f)), barHeight * 0.5f);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }
}
