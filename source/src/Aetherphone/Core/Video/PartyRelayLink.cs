using Aetherphone.Core.Telephony;

namespace Aetherphone.Core.Video;

internal enum PartyInviteOutcome : byte
{
    Joining,
    ServerSaved,
    NotAnInvite,
    ServerMissing,
}

internal readonly record struct PartyRelayLink(string Server, string Invite, string Code)
{
    internal const char Separator = '#';
    internal const int MaxLength = 256;

    private const string SeparatorText = "#";
    private const int MaxConnectionLength = MaxLength - 1 - PartyCode.Length;
    private const int MaxInviteLength = 128;
    private const string SchemeMark = "://";
    private const string SecureScheme = "https://";
    private const string InviteSymbols = "-._~";

    internal static bool TryParse(string? input, out PartyRelayLink link)
    {
        link = default;
        var text = input.AsSpan().Trim();
        if (text.IsEmpty || text.Length > MaxLength)
        {
            return false;
        }

        var first = text.IndexOf(Separator);
        if (first <= 0)
        {
            return false;
        }

        var rest = text[(first + 1)..];
        var second = rest.IndexOf(Separator);
        var connectionLength = second < 0 ? text.Length : first + 1 + second;
        if (connectionLength > MaxConnectionLength)
        {
            return false;
        }

        if (DecodeInvite((second < 0 ? rest : rest[..second]).Trim()) is not { } invite)
        {
            return false;
        }

        var code = string.Empty;
        if (second >= 0)
        {
            code = PartyCode.Normalize(rest[(second + 1)..].Trim().ToString());
            if (code.Length == 0)
            {
                return false;
            }
        }

        if (NormalizeServer(text[..first].Trim()) is not { } server)
        {
            return false;
        }

        link = new PartyRelayLink(server, invite, code);
        return true;
    }

    internal static string Format(string server, string invite, string code)
    {
        var shownServer = DisplayServer(server);
        var encodedInvite = Uri.EscapeDataString(invite.Trim());
        if (shownServer.Length == 0 || encodedInvite.Length == 0)
        {
            return string.Empty;
        }

        return code.Length == 0
            ? string.Concat(shownServer, SeparatorText, encodedInvite)
            : string.Concat(shownServer, SeparatorText, encodedInvite, SeparatorText, code);
    }

    internal static string DisplayServer(string server)
    {
        var shown = server.AsSpan().Trim().TrimEnd('/');
        if (shown.StartsWith(SecureScheme, StringComparison.OrdinalIgnoreCase))
        {
            shown = shown[SecureScheme.Length..];
        }

        return shown.ToString();
    }

    private static string? NormalizeServer(ReadOnlySpan<char> server)
    {
        if (server.IsEmpty || ContainsWhitespace(server))
        {
            return null;
        }

        var trimmed = server.TrimEnd('/');
        var url = trimmed.Contains(SchemeMark, StringComparison.Ordinal)
            ? trimmed.ToString()
            : string.Concat(SecureScheme, trimmed);
        return PartyRelay.IsUsableServer(url) ? url : null;
    }

    private static string? DecodeInvite(ReadOnlySpan<char> encoded)
    {
        if (encoded.IsEmpty || encoded.Length > MaxInviteLength || !IsEncodedInvite(encoded))
        {
            return null;
        }

        var invite = Uri.UnescapeDataString(encoded.ToString());
        if (invite.Length == 0 || char.IsWhiteSpace(invite[0]) || char.IsWhiteSpace(invite[^1])
            || Uri.EscapeDataString(invite).Length > encoded.Length)
        {
            return null;
        }

        return invite;
    }

    private static bool IsEncodedInvite(ReadOnlySpan<char> encoded)
    {
        for (var index = 0; index < encoded.Length; index++)
        {
            var character = encoded[index];
            if (char.IsAsciiLetterOrDigit(character) || InviteSymbols.Contains(character))
            {
                continue;
            }

            if (character != '%' || index + 2 >= encoded.Length
                || !char.IsAsciiHexDigit(encoded[index + 1]) || !char.IsAsciiHexDigit(encoded[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }

    private static bool ContainsWhitespace(ReadOnlySpan<char> text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return true;
            }
        }

        return false;
    }
}
