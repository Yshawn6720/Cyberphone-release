using Aetherphone.Core.Localization;
using Dalamud.Game.Text;

namespace Aetherphone.Core.GameChat;

internal enum ChannelCategory : byte
{
    Local,
    Group,
    Community,
    Linkshell,
    CrossWorld,
    Direct,
    System,
}

internal sealed class GameChannel
{
    public required int Index { get; init; }
    public required string Key { get; init; }
    public required XivChatType[] Kinds { get; init; }
    public required string Command { get; init; }
    public required LocString Label { get; init; }
    public required Vector4 Tint { get; init; }
    public required ChannelCategory Category { get; init; }
    public int Slot { get; init; }

    public bool CanSend => Command.Length > 0;

    public bool NeedsTarget => Category == ChannelCategory.Direct;

    public bool IsSlotted => Category is ChannelCategory.Linkshell or ChannelCategory.CrossWorld;
}
