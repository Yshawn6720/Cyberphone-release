using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Telephony;

internal static class RelayRefusalText
{
    private static FormattedBody? formatted;

    public static string Title(in RelayFailure failure, PartyRelayKind kind)
    {
        return failure.Refusal switch
        {
            RelayRefusal.RoomsFull => kind switch
            {
                PartyRelayKind.Call when failure.MaxRooms <= 0 => Loc.T(L.Phone.RoomsBusyTitle),
                PartyRelayKind.Call => Loc.T(L.Relay.CallRoomsFullTitle),
                _ => Loc.T(L.Relay.FullTitle),
            },
            RelayRefusal.RateLimited => Loc.T(L.Relay.RateLimitedTitle),
            RelayRefusal.AddressRooms => Loc.T(L.Relay.AddressRoomsTitle),
            _ => Loc.T(L.Phone.RoomUnsupportedTitle),
        };
    }

    public static string Body(in RelayFailure failure, PartyRelayKind kind)
    {
        return failure.Refusal switch
        {
            RelayRefusal.RoomsFull => kind switch
            {
                PartyRelayKind.Call when failure.MaxRooms <= 0 => Loc.T(L.Phone.RoomsBusyBody),
                PartyRelayKind.Call => Format(L.Relay.CallRoomsFullBody, failure.MaxRooms),
                PartyRelayKind.Jam => Loc.T(L.Relay.FullJamBody),
                PartyRelayKind.Game => Loc.T(L.Relay.FullGameBody),
                _ => Loc.T(L.Relay.FullPartyBody),
            },
            RelayRefusal.RateLimited => Format(L.Relay.RateLimitedBody, failure.RetryAfterSeconds),
            RelayRefusal.AddressRooms => Format(L.Relay.AddressRoomsBody, failure.MaxRooms),
            _ => Loc.T(L.Phone.RoomUnsupportedBody),
        };
    }

    private static string Format(LocString template, int argument)
    {
        var language = Loc.Current;
        var cached = Volatile.Read(ref formatted);
        if (cached is not null && ReferenceEquals(cached.Key, template.Key) && cached.Argument == argument
            && ReferenceEquals(cached.Language, language))
        {
            return cached.Text;
        }

        var text = Loc.T(template, argument);
        Volatile.Write(ref formatted, new FormattedBody(template.Key, argument, language, text));
        return text;
    }

    private sealed record FormattedBody(string Key, int Argument, LanguageInfo Language, string Text);
}
