using Aetherphone.Core.Maps;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Jam;

internal readonly record struct JamLocation(uint TerritoryId, uint WorldId);

internal sealed partial class JamSession
{
    private readonly JamNearbyCadence nearbyCadence = new();
    private readonly RelayNearbyQuery relayNearby;
    private JamNearbyJam[] nearbyJams = Array.Empty<JamNearbyJam>();
    private JamLocation reportedLocation;
    private long nearbyInterestAt;
    private bool nearbyWanted;

    public bool Discoverable { get; private set; }
    public ReadOnlySpan<JamNearbyJam> NearbyJams => nearbyJams;
    public int NearbyVersion { get; private set; }

    public bool NearbyLoading => RelayMode && relayNearby.Loading;

    public bool NearbyUnsupported => RelayMode && relayNearby.Unsupported;

    public bool CanRefreshNearby =>
        RelayMode && Mode == JamMode.Idle && relayNearby.CanAsk(Environment.TickCount64);

    public void WantNearby()
    {
        nearbyInterestAt = Environment.TickCount64;
    }

    public void RefreshNearby()
    {
        if (!RelayMode || Mode != JamMode.Idle)
        {
            return;
        }

        var location = locate();
        relayNearby.TryAsk(location.TerritoryId, location.WorldId, Environment.TickCount64);
    }

    public void SetDiscoverable(bool discoverable)
    {
        if (Mode != JamMode.Hosting)
        {
            return;
        }

        signals.Settings(null, null, null, discoverable);
        nearbyCadence.Reset();
        if (discoverable && RelayMode)
        {
            ReportRelayLocation(Environment.TickCount64);
        }
    }

    private void TickNearby(long now)
    {
        if (RelayMode)
        {
            TickRelayNearby(now);
            return;
        }

        if (!JamNearbyCadence.Wanted(now, nearbyInterestAt, IsHost && Discoverable))
        {
            if (nearbyWanted)
            {
                nearbyWanted = false;
                nearbyCadence.Reset();
                SetNearby(Array.Empty<JamNearbyJam>());
            }

            return;
        }

        nearbyWanted = true;
        if (!nearbyCadence.MayReport(now) || !signals.Connected || session.CurrentUser is null
            || disconnectedSinceTicks != 0)
        {
            return;
        }

        var location = locate();
        if (!nearbyCadence.ShouldReport(now, location.TerritoryId, location.WorldId))
        {
            return;
        }

        signals.Nearby(location.TerritoryId, location.WorldId);
        nearbyCadence.MarkSent(now, location.TerritoryId, location.WorldId);
    }

    // The relay lists rooms over HTTP and keeps the host's last jam.nearby until the host leaves, so browsing opens
    // no socket and a discoverable host only reports a new zone, never on a repeating timer.
    private void TickRelayNearby(long now)
    {
        if (IsHost && Discoverable)
        {
            ReportRelayLocation(now);
        }

        if (!JamNearbyCadence.Wanted(now, nearbyInterestAt, false))
        {
            nearbyWanted = false;
            SetNearby(Array.Empty<JamNearbyJam>());
            return;
        }

        var browsing = Mode == JamMode.Idle;
        if (browsing && !nearbyWanted)
        {
            if (relayNearby.CanAsk(now))
            {
                relayNearby.TryTake(out _);
            }

            RefreshNearby();
        }

        nearbyWanted = browsing;
        if (browsing && relayNearby.TryTake(out var message))
        {
            OnNearbyRoster(message);
        }
    }

    private void ReportRelayLocation(long now)
    {
        if (!signals.Connected || disconnectedSinceTicks != 0 || awaitingSinceTicks != 0
            || !nearbyCadence.MayReport(now))
        {
            return;
        }

        var location = locate();
        if (location.TerritoryId == 0 || location.WorldId == 0 || location == reportedLocation)
        {
            return;
        }

        signals.Nearby(location.TerritoryId, location.WorldId);
        nearbyCadence.MarkSent(now, location.TerritoryId, location.WorldId);
        reportedLocation = location;
    }

    private void OnNearbyRoster(CallControl message)
    {
        if (nearbyWanted)
        {
            SetNearby(JamWire.ToNearby(message.NearbyJams, jamId));
        }
    }

    private void ApplyDiscoverable(bool? discoverable)
    {
        var next = discoverable ?? Discoverable;
        if (next == Discoverable)
        {
            return;
        }

        Discoverable = next;
        nearbyCadence.Reset();
    }

    private void SetNearby(JamNearbyJam[] jams)
    {
        if (jams.Length == 0 && nearbyJams.Length == 0)
        {
            return;
        }

        nearbyJams = jams;
        NearbyVersion++;
    }

    private static JamLocation LocateLocalPlayer()
    {
        return new JamLocation(Plugin.ClientState.TerritoryType, LocationShare.CurrentWorldId());
    }
}
