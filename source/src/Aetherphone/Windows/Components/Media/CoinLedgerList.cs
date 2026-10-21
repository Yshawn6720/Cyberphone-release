using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class CoinLedgerList
{
    public const int FilterAll = 0;
    public const int FilterEarned = 1;
    public const int FilterSpent = 2;

    public static void Draw(CoinLedgerEntryDto[] entries, int filter, PhoneTheme theme, AppSkin ui)
    {
        var index = 0;
        while (index < entries.Length)
        {
            if (!Matches(entries[index], filter))
            {
                index++;
                continue;
            }

            var dayStart = index;
            var count = 0;
            var probe = dayStart;
            while (probe < entries.Length
                && TimeText.SameLocalDay(entries[dayStart].CreatedAtUnix, entries[probe].CreatedAtUnix))
            {
                if (Matches(entries[probe], filter))
                {
                    count++;
                }

                probe++;
            }

            ui.SectionHeading(TimeText.DayLabel(entries[dayStart].CreatedAtUnix), 8f);
            var card = GroupCard.Begin(theme, count);
            for (var entryIndex = dayStart; entryIndex < probe; entryIndex++)
            {
                var entry = entries[entryIndex];
                if (!Matches(entry, filter))
                {
                    continue;
                }

                var appId = entry.App.Length == 0 ? "coin" : entry.App;
                var amountText = (entry.Amount >= 0 ? "+" : "") + NumberText.Group(entry.Amount);
                SettingsRow.AppLink(card.NextRow(), appId, AppAccents.For(appId),
                    Loc.T(CoinRuleLabels.For(entry.RuleId)), amountText, theme, entry.Id);
            }

            card.End();
            index = probe;
        }
    }

    public static int CountMatching(CoinLedgerEntryDto[] entries, int filter)
    {
        var count = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (Matches(entries[index], filter))
            {
                count++;
            }
        }

        return count;
    }

    private static bool Matches(in CoinLedgerEntryDto entry, int filter)
    {
        return filter switch
        {
            FilterEarned => entry.Amount > 0,
            FilterSpent => entry.Amount < 0,
            _ => true,
        };
    }
}
