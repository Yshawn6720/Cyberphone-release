using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class CoinEarnFloats : IDisposable
{
    private const int MaxQueued = 8;

    private readonly CoinStore store;
    private readonly CoinFloat floats = new();
    private readonly object gate = new();
    private readonly List<long> queued = new(MaxQueued);

    public CoinEarnFloats(CoinStore store)
    {
        this.store = store;
        store.Earned += OnEarned;
    }

    private void OnEarned(long delta, CoinLedgerEntryDto[] entries)
    {
        if (delta <= 0)
        {
            return;
        }

        lock (gate)
        {
            if (queued.Count < MaxQueued)
            {
                queued.Add(delta);
            }
        }

        UiFeedback.Play(UiSound.Payout);
    }

    public void Draw(Rect screen, PhoneTheme theme, float delta)
    {
        var scale = UiScale.Current;
        lock (gate)
        {
            for (var index = 0; index < queued.Count; index++)
            {
                var anchor = new Vector2(screen.Center.X, screen.Min.Y + (108f + index * 24f) * scale);
                floats.Spawn(Loc.T(L.Coin.CheckInReward, NumberText.Group(queued[index])), anchor);
            }

            queued.Clear();
        }

        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        floats.Draw(drawList, AppAccents.For("coin"), theme.TextMuted, delta);
        drawList.PopClipRect();
    }

    public void Dispose()
    {
        store.Earned -= OnEarned;
    }
}
