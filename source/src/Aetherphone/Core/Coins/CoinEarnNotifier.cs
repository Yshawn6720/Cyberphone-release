using System.Text;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Coins;

internal sealed class CoinEarnNotifier : IDisposable
{
    public const string GroupKey = "coin:daily";

    private const int MaxNamedRules = 3;

    private readonly CoinStore store;
    private readonly NotificationService notifications;

    public CoinEarnNotifier(CoinStore store, NotificationService notifications)
    {
        this.store = store;
        this.notifications = notifications;
        store.Earned += OnEarned;
    }

    private void OnEarned(long delta, CoinLedgerEntryDto[] entries)
    {
        if (delta <= 0)
        {
            return;
        }

        var body = new StringBuilder();
        var named = 0;
        for (var index = 0; index < entries.Length && named < MaxNamedRules; index++)
        {
            if (entries[index].Amount <= 0)
            {
                continue;
            }

            if (named > 0)
            {
                body.Append(", ");
            }

            body.Append(Loc.T(CoinRuleLabels.For(entries[index].RuleId)));
            body.Append(" +");
            body.Append(NumberText.Group(entries[index].Amount));
            named++;
        }

        var remaining = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].Amount > 0)
            {
                remaining++;
            }
        }

        remaining -= named;
        if (remaining > 0)
        {
            body.Append(' ');
            body.Append(Loc.T(L.Coin.RollupMore, remaining));
        }

        notifications.Notify(new PhoneNotification(
            "coin",
            Loc.T(L.Coin.RollupTitle, NumberText.Group(delta)),
            Loc.T(L.Coin.RollupBody, body.ToString()),
            DateTime.Now,
            AppAccents.For("coin"),
            GroupKey));
    }

    public void Dispose()
    {
        store.Earned -= OnEarned;
    }
}
