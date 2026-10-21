using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PartyRelayLinkTests
{
    [Fact]
    public void AConnectionCodeAssumesHttpsAndCarriesNoRoom()
    {
        Assert.True(PartyRelayLink.TryParse("  party.example.com#K7M2Q9XR4T ", out var link));

        Assert.Equal("https://party.example.com", link.Server);
        Assert.Equal("K7M2Q9XR4T", link.Invite);
        Assert.Equal(string.Empty, link.Code);
    }

    [Fact]
    public void APartyInviteNormalizesItsRoomCode()
    {
        Assert.True(PartyRelayLink.TryParse("https://party.example.com/#K7M2Q9XR4T#abc 123", out var link));

        Assert.Equal("https://party.example.com", link.Server);
        Assert.Equal("ABC123", link.Code);
    }

    [Fact]
    public void FormattedInvitesParseBackToTheSameServer()
    {
        var invite = PartyRelayLink.Format("https://party.example.com/relay/", "K7M2Q9XR4T", "ABC123");

        Assert.Equal("party.example.com/relay#K7M2Q9XR4T#ABC123", invite);
        Assert.True(PartyRelayLink.TryParse(invite, out var link));
        Assert.Equal("https://party.example.com/relay", link.Server);
        Assert.Equal("K7M2Q9XR4T", link.Invite);
        Assert.Equal("ABC123", link.Code);
    }

    [Fact]
    public void PlainHttpIsOnlyAcceptedForLoopback()
    {
        Assert.True(PartyRelayLink.TryParse("http://127.0.0.1:8787#K7M2", out var loopback));
        Assert.Equal("http://127.0.0.1:8787#K7M2", PartyRelayLink.Format(loopback.Server, loopback.Invite, string.Empty));

        Assert.False(PartyRelayLink.TryParse("http://party.example.com#K7M2", out _));
    }

    [Fact]
    public void AnUnsetServerFormatsAsEmpty()
    {
        Assert.Equal(string.Empty, PartyRelayLink.Format(string.Empty, "K7M2", string.Empty));
        Assert.Equal(string.Empty, PartyRelayLink.Format("https://party.example.com", "  ", string.Empty));
    }

    [Theory]
    [InlineData("K7#ABC123")]
    [InlineData("K7%M2")]
    [InlineData("K7 M2/Q9")]
    public void InvitesWithReservedCharactersSurviveARoundTrip(string invite)
    {
        var connection = PartyRelayLink.Format("https://party.example.com", invite, string.Empty);
        var partyInvite = PartyRelayLink.Format("https://party.example.com", invite, "ABC123");

        Assert.True(PartyRelayLink.TryParse(connection, out var connectionLink));
        Assert.Equal(invite, connectionLink.Invite);
        Assert.Equal(string.Empty, connectionLink.Code);
        Assert.True(PartyRelayLink.TryParse(partyInvite, out var partyLink));
        Assert.Equal(invite, partyLink.Invite);
        Assert.Equal("ABC123", partyLink.Code);
    }

    [Fact]
    public void TheLongestConnectionCodeStillMakesAJoinablePartyInvite()
    {
        var server = string.Concat("party.example.com/", new string('a', 120 - "party.example.com/".Length));
        var longest = string.Concat(server, "#", new string('K', 249 - server.Length - 1));

        Assert.True(PartyRelayLink.TryParse(longest, out var link));
        Assert.True(PartyRelayLink.TryParse(PartyRelayLink.Format(link.Server, link.Invite, "ABC123"), out var party));
        Assert.Equal("ABC123", party.Code);
        Assert.False(PartyRelayLink.TryParse(string.Concat(longest, "K"), out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("party.example.com")]
    [InlineData("#K7M2")]
    [InlineData("party.example.com#")]
    [InlineData("party.example.com#K7 M2")]
    [InlineData("party example.com#K7M2")]
    [InlineData("party.example.com#K7M2#ABC12")]
    [InlineData("party.example.com#K7M2#")]
    public void RejectsInvitesThatCannotReachAServer(string input)
    {
        Assert.False(PartyRelayLink.TryParse(input, out _));
    }
}
