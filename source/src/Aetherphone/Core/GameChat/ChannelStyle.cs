namespace Aetherphone.Core.GameChat;

internal sealed class ChannelStyle
{
    public const int IncomingNameSlot = 0;
    public const int IncomingBodySlot = 1;
    public const int OutgoingNameSlot = 2;
    public const int OutgoingBodySlot = 3;
    public const int InkSlotCount = 4;

    public uint IncomingName { get; set; }

    public uint IncomingBody { get; set; }

    public uint OutgoingName { get; set; }

    public uint OutgoingBody { get; set; }

    public bool NeverUnread { get; set; }

    public bool HideOutgoing { get; set; }

    public bool HideFromGameChat { get; set; }

    public bool IsDefault =>
        IncomingName == 0u && IncomingBody == 0u && OutgoingName == 0u && OutgoingBody == 0u &&
        !NeverUnread && !HideOutgoing && !HideFromGameChat;

    public uint Ink(int slot) => slot switch
    {
        IncomingNameSlot => IncomingName,
        IncomingBodySlot => IncomingBody,
        OutgoingNameSlot => OutgoingName,
        OutgoingBodySlot => OutgoingBody,
        _ => 0u,
    };

    public void SetInk(int slot, uint packed)
    {
        switch (slot)
        {
            case IncomingNameSlot:
                IncomingName = packed;
                break;
            case IncomingBodySlot:
                IncomingBody = packed;
                break;
            case OutgoingNameSlot:
                OutgoingName = packed;
                break;
            case OutgoingBodySlot:
                OutgoingBody = packed;
                break;
        }
    }

    public void CopyFrom(ChannelStyle other)
    {
        IncomingName = other.IncomingName;
        IncomingBody = other.IncomingBody;
        OutgoingName = other.OutgoingName;
        OutgoingBody = other.OutgoingBody;
        NeverUnread = other.NeverUnread;
        HideOutgoing = other.HideOutgoing;
        HideFromGameChat = other.HideFromGameChat;
    }

    public void Clear()
    {
        IncomingName = 0u;
        IncomingBody = 0u;
        OutgoingName = 0u;
        OutgoingBody = 0u;
        NeverUnread = false;
        HideOutgoing = false;
        HideFromGameChat = false;
    }
}

internal static class ChannelInk
{
    private const float ByteScale = 255f;

    public static Vector4 Unpack(uint packed) =>
        new(((packed >> 16) & 0xFFu) / ByteScale, ((packed >> 8) & 0xFFu) / ByteScale, (packed & 0xFFu) / ByteScale,
            ((packed >> 24) & 0xFFu) / ByteScale);

    public static uint Pack(Vector4 color) =>
        (Channel(color.W) << 24) | (Channel(color.X) << 16) | (Channel(color.Y) << 8) | Channel(color.Z);

    private static uint Channel(float value) => (uint)Math.Clamp(MathF.Round(value * ByteScale), 0f, ByteScale);
}
