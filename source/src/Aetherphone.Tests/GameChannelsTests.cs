using Aetherphone.Core.GameChat;
using Dalamud.Game.Text;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GameChannelsTests
{
    [Theory]
    [InlineData(XivChatType.Say, "say")]
    [InlineData(XivChatType.Shout, "shout")]
    [InlineData(XivChatType.Yell, "yell")]
    [InlineData(XivChatType.CustomEmote, "emote")]
    [InlineData(XivChatType.StandardEmote, "emote")]
    [InlineData(XivChatType.TellIncoming, "tell")]
    [InlineData(XivChatType.TellOutgoing, "tell")]
    [InlineData(XivChatType.Party, "party")]
    [InlineData(XivChatType.CrossParty, "party")]
    [InlineData(XivChatType.Alliance, "alliance")]
    [InlineData(XivChatType.PvPTeam, "pvpteam")]
    [InlineData(XivChatType.FreeCompany, "fc")]
    [InlineData(XivChatType.NoviceNetwork, "novice")]
    [InlineData(XivChatType.Echo, "echo")]
    [InlineData(XivChatType.Ls1, "ls1")]
    [InlineData(XivChatType.Ls8, "ls8")]
    [InlineData(XivChatType.CrossLinkShell1, "cwls1")]
    [InlineData(XivChatType.CrossLinkShell2, "cwls2")]
    [InlineData(XivChatType.CrossLinkShell8, "cwls8")]
    public void ResolvesEveryConversationalChatType(XivChatType kind, string expectedKey)
    {
        Assert.True(GameChannels.TryResolve(kind, out var channel));
        Assert.Equal(expectedKey, channel.Key);
    }

    [Theory]
    [InlineData(XivChatType.Debug)]
    [InlineData(XivChatType.Urgent)]
    [InlineData(XivChatType.SystemError)]
    [InlineData(XivChatType.NPCDialogueAnnouncements)]
    [InlineData(XivChatType.NPCDialogue)]
    public void IgnoresChannelsOutsideScope(XivChatType kind) =>
        Assert.False(GameChannels.TryResolve(kind, out _));

    [Fact]
    public void EveryChatTypeMapsToOneChannelOnly()
    {
        var owners = new Dictionary<XivChatType, string>();
        var all = GameChannels.All;
        for (var index = 0; index < all.Length; index++)
        {
            var channel = all[index];
            for (var kindIndex = 0; kindIndex < channel.Kinds.Length; kindIndex++)
            {
                var kind = channel.Kinds[kindIndex];
                Assert.False(owners.TryGetValue(kind, out var existing),
                    $"{kind} is claimed by both {existing} and {channel.Key}");
                owners[kind] = channel.Key;
            }
        }
    }

    [Fact]
    public void KeysAndIndexesAreUnique()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var all = GameChannels.All;
        for (var index = 0; index < all.Length; index++)
        {
            Assert.True(keys.Add(all[index].Key), $"duplicate key {all[index].Key}");
            Assert.Equal(index, all[index].Index);
            Assert.Same(all[index], GameChannels.ByIndex(index));
        }
    }

    [Fact]
    public void SlottedChannelsCarryMatchingCommands()
    {
        for (var slot = 0; slot < GameChannels.LinkshellSlots; slot++)
        {
            Assert.True(GameChannels.TryByKey($"ls{slot + 1}", out var linkshell));
            Assert.Equal($"/linkshell{slot + 1}", linkshell.Command);
            Assert.Equal(slot, linkshell.Slot);
            Assert.True(linkshell.IsSlotted);

            Assert.True(GameChannels.TryByKey($"cwls{slot + 1}", out var crossWorld));
            Assert.Equal($"/cwlinkshell{slot + 1}", crossWorld.Command);
            Assert.Equal(slot, crossWorld.Slot);
        }
    }

    [Fact]
    public void OnlySystemMessagesAreReadOnly()
    {
        var all = GameChannels.All;
        for (var index = 0; index < all.Length; index++)
        {
            var channel = all[index];
            var expected = !string.Equals(channel.Key, "system", StringComparison.Ordinal);
            Assert.Equal(expected, channel.CanSend);
        }
    }

    [Fact]
    public void OnlyTellNeedsATarget()
    {
        var all = GameChannels.All;
        for (var index = 0; index < all.Length; index++)
        {
            var expected = string.Equals(all[index].Key, GameChannels.TellKey, StringComparison.Ordinal);
            Assert.Equal(expected, all[index].NeedsTarget);
        }
    }

    [Fact]
    public void BudgetLeavesRoomForTheCommandPrefix()
    {
        Assert.True(GameChannels.TryByKey(GameChannels.SayKey, out var say));
        Assert.Equal(ChatSend.MaxBytes - "/say ".Length, ChatSend.Budget(say, string.Empty));

        Assert.True(GameChannels.TryByKey(GameChannels.TellKey, out var tell));
        const string target = "Aiko Braveheart@Odin";
        Assert.Equal(ChatSend.MaxBytes - "/tell ".Length - target.Length - 1,
            ChatSend.Budget(tell, target));

        Assert.True(GameChannels.TryByKey("system", out var system));
        Assert.Equal(0, ChatSend.Budget(system, string.Empty));
    }

    [Fact]
    public void BudgetCountsTargetBytesNotCharacters()
    {
        Assert.True(GameChannels.TryByKey(GameChannels.TellKey, out var tell));
        var wide = ChatSend.Budget(tell, "Aiko@Odin");
        var wider = ChatSend.Budget(tell, "アイコ@Odin");
        Assert.True(wider < wide);
    }
}
