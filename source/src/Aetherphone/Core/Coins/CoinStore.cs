using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Core.Coins;

internal sealed class CoinStore : IDisposable
{
    private const long WalletRefreshMilliseconds = 60_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly StoreWork work = new("Coin");

    private volatile CoinWalletDto? wallet;
    private volatile CoinLedgerEntryDto[] entries = Array.Empty<CoinLedgerEntryDto>();
    private volatile string? cursor;
    private volatile bool loadedOnce;
    private volatile bool loadingMore;
    private volatile bool endReached;
    private volatile bool checkingIn;
    private volatile bool purchasing;
    private CoinAwardDto? checkInResult;
    private CoinPurchaseResult? purchaseResult;
    private long walletLoadedAtTick;
    private long walletAttemptedAtTick;
    private long ledgerPageRetryAtTick;
    private int fetchingWallet;
    private int fetchingLedger;
    private string? lastAccountId;
    private long lastSeenBalance = -1;

    public event Action<long, CoinLedgerEntryDto[]>? Earned;

    public event Action? WalletRefreshed;

    public CoinStore(AethernetSession session, CoinsClient coins)
    {
        this.session = session;
        this.coins = coins;
        session.Changed += OnSessionChanged;
    }

    public CoinWalletDto? Wallet => wallet;

    public CoinLedgerEntryDto[] Entries => entries;

    public bool LoadedOnce => loadedOnce;

    public bool LoadingMore => loadingMore;

    public bool EndReached => endReached;

    public bool CheckingIn => checkingIn;

    public bool Purchasing => purchasing;

    public void EnsureFresh()
    {
        RefreshWallet(WalletRefreshMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref walletLoadedAtTick, 0);
        Interlocked.Exchange(ref walletAttemptedAtTick, 0);
        RefreshWallet(0);
        ReloadLedger();
    }

    public void AbsorbLocalAward(long balance)
    {
        if (balance < 0)
        {
            return;
        }

        Interlocked.Exchange(ref lastSeenBalance, balance);
        var next = BalanceAbsorbedInto(wallet, balance);
        if (next is not null)
        {
            wallet = next;
        }

        Interlocked.Exchange(ref walletLoadedAtTick, 0);
        Interlocked.Exchange(ref walletAttemptedAtTick, 0);
        RefreshWallet(0);
    }

    internal static CoinWalletDto? BalanceAbsorbedInto(CoinWalletDto? current, long balance)
    {
        if (current is null || current.Balance == balance)
        {
            return null;
        }

        return current with { Balance = balance };
    }

    public CoinAwardDto? TakeCheckInResult()
    {
        return Interlocked.Exchange(ref checkInResult, null);
    }

    public CoinPurchaseResult? TakePurchaseResult()
    {
        return Interlocked.Exchange(ref purchaseResult, null);
    }

    public void CheckIn()
    {
        if (checkingIn || !session.IsSignedIn)
        {
            return;
        }

        checkingIn = true;
        work.Run("check-in", async token =>
        {
            var result = await coins.CheckInAsync(token).ConfigureAwait(false);
            Interlocked.Exchange(ref checkInResult, result);
            if (result is { Granted: true })
            {
                Interlocked.Exchange(ref lastSeenBalance, result.Balance);
            }

            Interlocked.Exchange(ref walletLoadedAtTick, 0);
            Interlocked.Exchange(ref walletAttemptedAtTick, 0);
            RefreshWallet(0);
            ReloadLedger();
        }, () => checkingIn = false);
    }

    public void Purchase(string skuId, long expectedPrice)
    {
        if (purchasing || !session.IsSignedIn)
        {
            return;
        }

        purchasing = true;
        work.Run("purchase", async token =>
        {
            var result = await coins.PurchaseAsync(skuId, expectedPrice, token).ConfigureAwait(false);
            Interlocked.Exchange(ref purchaseResult, result);
            if (result is { Purchased: true })
            {
                Interlocked.Exchange(ref lastSeenBalance, result.Balance);
            }

            Interlocked.Exchange(ref walletLoadedAtTick, 0);
            Interlocked.Exchange(ref walletAttemptedAtTick, 0);
            RefreshWallet(0);
            ReloadLedger();
        }, () => purchasing = false);
    }

    public void LoadMore()
    {
        if (loadingMore || endReached || cursor is null || !session.IsSignedIn)
        {
            return;
        }

        if (Environment.TickCount64 < Interlocked.Read(ref ledgerPageRetryAtTick))
        {
            return;
        }

        if (Interlocked.Exchange(ref fetchingLedger, 1) != 0)
        {
            return;
        }

        loadingMore = true;
        var requestCursor = cursor;
        Interlocked.Exchange(ref ledgerPageRetryAtTick, Environment.TickCount64 + RetryAfterAttemptMilliseconds);
        work.Run("ledger page", async token =>
        {
            var page = await coins.LedgerAsync(requestCursor, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            var current = entries;
            var merged = new CoinLedgerEntryDto[current.Length + page.Items.Length];
            current.CopyTo(merged, 0);
            page.Items.CopyTo(merged, current.Length);
            entries = merged;
            cursor = page.NextCursor;
            endReached = page.NextCursor is null || page.Items.Length == 0;
            Interlocked.Exchange(ref ledgerPageRetryAtTick, 0);
        }, () =>
        {
            loadingMore = false;
            Interlocked.Exchange(ref fetchingLedger, 0);
        });
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (!string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            lastAccountId = accountId;
            wallet = null;
            entries = Array.Empty<CoinLedgerEntryDto>();
            cursor = null;
            loadedOnce = false;
            endReached = false;
            Interlocked.Exchange(ref ledgerPageRetryAtTick, 0);
            Interlocked.Exchange(ref checkInResult, null);
            Interlocked.Exchange(ref purchaseResult, null);
            Interlocked.Exchange(ref walletLoadedAtTick, 0);
            Interlocked.Exchange(ref walletAttemptedAtTick, 0);
            Interlocked.Exchange(ref lastSeenBalance, -1);
            return;
        }

        var balance = session.CurrentUser?.Coins ?? -1;
        var previous = Interlocked.Exchange(ref lastSeenBalance, balance);
        if (previous < 0 || balance <= previous)
        {
            return;
        }

        var delta = balance - previous;
        work.Run("earn delta", async token =>
        {
            var page = await coins.LedgerAsync(null, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            var fresh = CollectEarnDelta(page.Items, delta, out var celebratedDelta);
            if (celebratedDelta > 0)
            {
                Earned?.Invoke(celebratedDelta, fresh);
            }

            Interlocked.Exchange(ref walletLoadedAtTick, 0);
            RefreshWallet(0);
        });
    }

    internal static CoinLedgerEntryDto[] CollectEarnDelta(CoinLedgerEntryDto[] items, long delta,
        out long celebratedDelta)
    {
        var count = 0;
        long running = 0;
        while (count < items.Length && running < delta)
        {
            if (items[count].Amount > 0)
            {
                running += items[count].Amount;
            }

            count++;
        }

        celebratedDelta = 0;
        var celebrated = 0;
        for (var index = 0; index < count; index++)
        {
            if (CelebratesEarn(items[index]))
            {
                celebrated++;
            }
        }

        var slice = new CoinLedgerEntryDto[celebrated];
        var write = 0;
        for (var index = 0; index < count; index++)
        {
            if (!CelebratesEarn(items[index]))
            {
                continue;
            }

            if (items[index].Amount > 0)
            {
                celebratedDelta += items[index].Amount;
            }

            slice[write] = items[index];
            write++;
        }

        if (celebratedDelta > delta)
        {
            celebratedDelta = delta;
        }

        return slice;
    }

    private static bool CelebratesEarn(CoinLedgerEntryDto entry)
    {
        return !CasinoLedgerRules.SkipsEarnCelebration(entry.RuleId);
    }

    private void RefreshWallet(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastAttempt = Interlocked.Read(ref walletAttemptedAtTick);
        if (lastAttempt != 0 && now - lastAttempt < RetryAfterAttemptMilliseconds)
        {
            return;
        }

        var lastLoad = Interlocked.Read(ref walletLoadedAtTick);
        if (lastLoad != 0 && now - lastLoad < refreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetchingWallet, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref walletAttemptedAtTick, now);
        work.Run("wallet refresh", async token =>
        {
            var fresh = await coins.WalletAsync(token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            wallet = fresh;
            Interlocked.Exchange(ref lastSeenBalance, fresh.Balance);
            Interlocked.Exchange(ref walletLoadedAtTick, Environment.TickCount64);
            if (!loadedOnce)
            {
                ReloadLedger();
            }

            WalletRefreshed?.Invoke();
        }, () => Interlocked.Exchange(ref fetchingWallet, 0));
    }

    private void ReloadLedger()
    {
        if (Interlocked.Exchange(ref fetchingLedger, 1) != 0)
        {
            return;
        }

        work.Run("ledger reload", async token =>
        {
            var page = await coins.LedgerAsync(null, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            entries = page.Items;
            cursor = page.NextCursor;
            endReached = page.NextCursor is null;
            loadedOnce = true;
            Interlocked.Exchange(ref ledgerPageRetryAtTick, 0);
        }, () => Interlocked.Exchange(ref fetchingLedger, 0));
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
