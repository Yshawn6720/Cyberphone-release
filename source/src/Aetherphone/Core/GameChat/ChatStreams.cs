namespace Aetherphone.Core.GameChat;

internal static class ChatStreams
{
    private const string TellPrefix = "tell:";

    public static string ForChannel(string channelKey) => channelKey;

    public static string ForTell(string counterpart) => string.Concat(TellPrefix, counterpart.ToLowerInvariant());

    public static bool IsTell(string streamKey) => streamKey.StartsWith(TellPrefix, StringComparison.Ordinal);

    public static string TellTarget(string streamKey) =>
        IsTell(streamKey) ? streamKey[TellPrefix.Length..] : string.Empty;
}
