namespace Aetherphone.Core.GameChat;

internal sealed class SentMessage
{
    public string ChannelKey { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public long SentAt { get; set; }
}
