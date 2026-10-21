using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Coin;

internal sealed class CoinLedgerText
{
    public const int FilterAll = 0;
    public const int FilterEarned = 1;
    public const int FilterSpent = 2;

    private CoinLedgerEntryDto[] source = Array.Empty<CoinLedgerEntryDto>();
    private string[] titles = Array.Empty<string>();
    private string[] amounts = Array.Empty<string>();
    private string[] subtitles = Array.Empty<string>();
    private string[] days = Array.Empty<string>();
    private string[] stamps = Array.Empty<string>();
    private CultureInfo? culture;
    private int timeFormat = -1;
    private DateTime today;
    private int lastFound = -1;

    public CoinLedgerEntryDto[] Entries => source;

    public void Sync(CoinLedgerEntryDto[] entries)
    {
        var now = DateTime.Now.Date;
        if (ReferenceEquals(entries, source) && ReferenceEquals(culture, Loc.Culture) &&
            timeFormat == TimeText.FormatVersion && now == today)
        {
            return;
        }

        source = entries;
        culture = Loc.Culture;
        timeFormat = TimeText.FormatVersion;
        today = now;
        lastFound = -1;
        titles = new string[entries.Length];
        amounts = new string[entries.Length];
        subtitles = new string[entries.Length];
        days = new string[entries.Length];
        stamps = new string[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            titles[index] = Loc.T(CoinRuleLabels.For(entry.RuleId));
            amounts[index] = Signed(entry.Amount);
            subtitles[index] = Loc.T(L.Coin.HistoryRowDetail, TimeText.Clock(entry.CreatedAtUnix),
                NumberText.Group(entry.BalanceAfter));
            days[index] = TimeText.DayLabel(entry.CreatedAtUnix);
            stamps[index] = Loc.T(L.Coin.EntryStamp, TimeText.DayLabel(entry.CreatedAtUnix),
                TimeText.Clock(entry.CreatedAtUnix));
        }
    }

    public string Title(int index) => titles[index];

    public string Amount(int index) => amounts[index];

    public string Subtitle(int index) => subtitles[index];

    public string Day(int index) => days[index];

    public string Stamp(int index) => stamps[index];

    public int Find(string entryId)
    {
        if (lastFound >= 0 && lastFound < source.Length &&
            string.Equals(source[lastFound].Id, entryId, StringComparison.Ordinal))
        {
            return lastFound;
        }

        for (var index = 0; index < source.Length; index++)
        {
            if (string.Equals(source[index].Id, entryId, StringComparison.Ordinal))
            {
                lastFound = index;
                return index;
            }
        }

        return -1;
    }

    public int Count(int filter)
    {
        var count = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (Matches(source[index], filter))
            {
                count++;
            }
        }

        return count;
    }

    public static bool Matches(CoinLedgerEntryDto entry, int filter) => filter switch
    {
        FilterEarned => entry.Amount > 0,
        FilterSpent => entry.Amount < 0,
        _ => true,
    };

    public static string Signed(long amount) =>
        amount > 0 ? "+" + NumberText.Group(amount) : NumberText.Group(amount);
}
