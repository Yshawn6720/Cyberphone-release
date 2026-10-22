using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RelayRefusalTextTests
{
    [Fact]
    public void TheAddressCapReadsTheSameForEveryRelayAndNamesTheLimit()
    {
        var failure = RelayFailure.FromRefusal(new CallControl
        {
            Type = SignalType.RelayRefused, Reason = "address_rooms", MaxRooms = 3,
        });

        Assert.Equal(RelayRefusal.AddressRooms, failure.Refusal);
        Assert.Equal(3, failure.MaxRooms);
        PartyRelayKind[] kinds = [PartyRelayKind.Party, PartyRelayKind.Jam, PartyRelayKind.Game, PartyRelayKind.Call];
        foreach (var kind in kinds)
        {
            Assert.Equal(Loc.T(L.Relay.AddressRoomsTitle), RelayRefusalText.Title(failure, kind));
            Assert.Equal(Loc.T(L.Relay.AddressRoomsBody, 3), RelayRefusalText.Body(failure, kind));
        }
    }
}
