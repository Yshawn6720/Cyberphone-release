using Aetherphone.Core;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float GoalStage = 52f;
    private const float GoalBarHeight = 6f;
    private const float GoalBarTrackAlpha = 0.14f;

    private CoinSavingGoal? CurrentGoal()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.IsNullOrEmpty(accountId))
        {
            return null;
        }

        return configuration.CoinSavingGoals.TryGetValue(accountId, out var goal) ? goal : null;
    }

    private bool IsGoal(string skuId) =>
        CurrentGoal() is { } goal && string.Equals(goal.SkuId, skuId, StringComparison.Ordinal);

    private void SetGoal(CoinSkuStyle sku, string categoryId)
    {
        var accountId = session.CurrentUser?.Id;
        if (string.IsNullOrEmpty(accountId))
        {
            return;
        }

        configuration.CoinSavingGoals[accountId] = new CoinSavingGoal
        {
            SkuId = sku.Id,
            CategoryId = categoryId,
            Name = sku.Name,
            Kind = sku.Kind,
            Payload = sku.Payload,
            Price = sku.Price,
        };
        configuration.Save();
    }

    private void ClearGoalFor(string skuId)
    {
        var accountId = session.CurrentUser?.Id;
        if (string.IsNullOrEmpty(accountId) || !IsGoal(skuId))
        {
            return;
        }

        configuration.CoinSavingGoals.Remove(accountId);
        configuration.Save();
    }

    private void SyncGoal(CoinSkuStyle sku)
    {
        var goal = CurrentGoal();
        if (goal is null || !string.Equals(goal.SkuId, sku.Id, StringComparison.Ordinal))
        {
            return;
        }

        if (sku.Owned)
        {
            ClearGoalFor(sku.Id);
            return;
        }

        var name = sku.Name;
        if (goal.Price == sku.Price && string.Equals(goal.Name, name, StringComparison.Ordinal))
        {
            return;
        }

        goal.Price = sku.Price;
        goal.Name = name;
        configuration.Save();
    }

    private float DrawSavingGoal(ImDrawListPtr drawList, Vector2 origin, float width, long balance, float scale)
    {
        var goal = CurrentGoal();
        if (goal is null)
        {
            return origin.Y;
        }

        var headerTop = origin.Y + CoinArt.SectionGap * scale;
        var cursorY = headerTop + CoinArt.SectionHeader(drawList, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Coin.SavingFor), ui.TitleInk, 0f, scale) + CoinArt.HeaderGap * scale;
        var pad = Metrics.Space.Lg * scale;
        var stage = GoalStage * scale;
        var height = pad * 2f + stage;
        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + height);
        var row = new Rect(min, max);
        CoinArt.Card(drawList, ui, min, max, scale);
        var hovered = CoinArt.RowInteraction(drawList, ui, row, scale);
        var stageMin = new Vector2(min.X + pad, min.Y + pad);
        DrawItemStage(drawList, new Rect(stageMin, stageMin + new Vector2(stage, stage)), goal.Kind, goal.Payload,
            scale);

        var textLeft = stageMin.X + stage + CoinArt.TextGap * scale;
        var textRight = max.X - pad;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var ready = balance >= goal.Price;
        var priceText = NumberText.Group(goal.Price);
        var priceWidth = CoinArt.PriceWidth(priceText, TextStyles.SubheadlineEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var statusHeight = Typography.LineHeight(TextStyles.Footnote);
        var blockHeight = titleHeight + CoinArt.LineGap * scale + statusHeight + Metrics.Space.Sm * scale +
                          GoalBarHeight * scale;
        var top = row.Center.Y - blockHeight * 0.5f;
        CoinArt.Price(drawList, new Vector2(textRight - priceWidth, top + (titleHeight -
                Typography.LineHeight(TextStyles.SubheadlineEmphasized)) * 0.5f), priceText, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(goal.Name, MathF.Max(1f, textWidth - priceWidth - CoinArt.ValueGap * scale),
                TextStyles.BodyEmphasized), ui.TitleInk, TextStyles.BodyEmphasized);
        var status = ready ? Loc.T(L.Coin.GoalReady) : texts.Remaining(CoinSavingGoal.Remaining(balance, goal.Price));
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight + CoinArt.LineGap * scale),
            Typography.FitText(status, textWidth, TextStyles.Footnote), ready ? ui.Accent : ui.MutedInk,
            TextStyles.Footnote);
        var barTop = top + titleHeight + CoinArt.LineGap * scale + statusHeight + Metrics.Space.Sm * scale;
        CoinArt.Bar(drawList, new Vector2(textLeft, barTop), new Vector2(textRight, barTop + GoalBarHeight * scale),
            CoinSavingGoal.Fraction(balance, goal.Price), Palette.WithAlpha(ui.TitleInk, GoalBarTrackAlpha), ui.Accent);
        if (UiInteract.Click(min, max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            catalog.EnsureFresh();
            router.Push(CoinRoute.Product(goal.CategoryId, goal.SkuId));
        }

        return max.Y;
    }
}
