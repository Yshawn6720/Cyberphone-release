namespace Aetherphone.Core.GameChat;

internal sealed class LinkpearlPopoutState
{
    public string Key { get; set; } = string.Empty;

    public List<string> Keys { get; set; } = new();

    public int Active { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public bool Collapsed { get; set; }
}
