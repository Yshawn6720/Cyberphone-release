using System.Numerics;
using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChannelStyleTests
{
    [Fact]
    public void AnUntouchedConfigurationHasNoOverrides()
    {
        var store = new ChannelStyleStore(new Configuration());

        Assert.False(store.HasOverrides);
        Assert.Null(store.For(GameChannels.SayKey));
        Assert.False(store.IsCustomized(GameChannels.SayKey));
        Assert.False(store.NeverUnread(GameChannels.SayKey));
        Assert.False(store.HidesOutgoing(GameChannels.SayKey));
    }

    [Fact]
    public void AStyleThatChangesNothingIsNotAnOverride()
    {
        var configuration = new Configuration();
        configuration.LinkpearlChannelStyles[GameChannels.SayKey] = new ChannelStyle();
        var store = new ChannelStyleStore(configuration);

        Assert.False(store.HasOverrides);
        Assert.Null(store.For(GameChannels.SayKey));
    }

    [Fact]
    public void OneCustomizedChannelLeavesEveryOtherChannelAlone()
    {
        var configuration = new Configuration();
        var store = new ChannelStyleStore(configuration);
        store.Apply(GameChannels.PartyKey, new ChannelStyle { IncomingBody = 0xFF32D74Bu });

        Assert.True(store.HasOverrides);
        Assert.Equal(0xFF32D74Bu, store.For(GameChannels.PartyKey)!.IncomingBody);
        Assert.Null(store.For(GameChannels.SayKey));
        Assert.Null(store.For(GameChannels.FreeCompanyKey));
    }

    [Fact]
    public void AKeyOutsideTheCatalogNeverResolves()
    {
        var configuration = new Configuration();
        configuration.LinkpearlChannelStyles["battle"] = new ChannelStyle { HideFromGameChat = true };
        var store = new ChannelStyleStore(configuration);

        Assert.False(store.HasOverrides);
        Assert.Null(store.For("battle"));
    }

    [Fact]
    public void ApplyAndResetRoundTripThroughTheConfiguration()
    {
        var configuration = new Configuration();
        var store = new ChannelStyleStore(configuration);
        store.Apply(GameChannels.SayKey, new ChannelStyle { HideOutgoing = true });

        Assert.True(store.HidesOutgoing(GameChannels.SayKey));
        Assert.True(configuration.LinkpearlChannelStyles.ContainsKey(GameChannels.SayKey));

        var reloaded = new ChannelStyleStore(configuration);
        Assert.True(reloaded.HidesOutgoing(GameChannels.SayKey));

        store.Reset(GameChannels.SayKey);
        Assert.False(store.HidesOutgoing(GameChannels.SayKey));
        Assert.False(configuration.LinkpearlChannelStyles.ContainsKey(GameChannels.SayKey));
    }

    [Fact]
    public void ApplyingADefaultStyleDropsTheEntry()
    {
        var configuration = new Configuration();
        var store = new ChannelStyleStore(configuration);
        store.Apply(GameChannels.SayKey, new ChannelStyle { NeverUnread = true });
        store.Apply(GameChannels.SayKey, new ChannelStyle());

        Assert.False(store.HasOverrides);
        Assert.False(configuration.LinkpearlChannelStyles.ContainsKey(GameChannels.SayKey));
    }

    [Fact]
    public void HidingFromGameChatNeedsTheMasterSwitchAndTheChannelFlag()
    {
        var configuration = new Configuration();
        var store = new ChannelStyleStore(configuration);
        Assert.True(GameChannels.TryByKey(GameChannels.PartyKey, out var party));

        Assert.False(store.HidesFromGameChat(party));

        store.Apply(GameChannels.PartyKey, new ChannelStyle { HideFromGameChat = true });
        Assert.False(store.HidesFromGameChat(party));

        configuration.LinkpearlHideHandledFromGameChat = true;
        Assert.True(store.HidesFromGameChat(party));

        Assert.True(GameChannels.TryByKey(GameChannels.SayKey, out var say));
        Assert.False(store.HidesFromGameChat(say));
    }

    [Fact]
    public void SystemChannelsCanNeverHideFromGameChat()
    {
        var configuration = new Configuration { LinkpearlHideHandledFromGameChat = true };
        var store = new ChannelStyleStore(configuration);
        store.Apply(GameChannels.SystemKey, new ChannelStyle { HideFromGameChat = true });
        store.Apply(GameChannels.EchoKey, new ChannelStyle { HideFromGameChat = true });
        Assert.True(GameChannels.TryByKey(GameChannels.SystemKey, out var system));
        Assert.True(GameChannels.TryByKey(GameChannels.EchoKey, out var echo));

        Assert.False(ChannelStyleStore.CanHideFromGameChat(system));
        Assert.False(ChannelStyleStore.CanHideFromGameChat(echo));
        Assert.False(store.HidesFromGameChat(system));
        Assert.False(store.HidesFromGameChat(echo));
    }

    [Fact]
    public void EverySendableConversationChannelCanOptIntoHiding()
    {
        var channels = GameChannels.All;
        for (var index = 0; index < channels.Length; index++)
        {
            var channel = channels[index];
            Assert.Equal(channel.Category != ChannelCategory.System,
                ChannelStyleStore.CanHideFromGameChat(channel));
        }
    }

    [Fact]
    public void InkPacksAndUnpacksEveryChannel()
    {
        var color = new Vector4(0.196f, 0.843f, 0.294f, 1f);
        var packed = ChannelInk.Pack(color);
        var restored = ChannelInk.Unpack(packed);

        Assert.Equal(0xFF32D74Bu, packed);
        Assert.Equal(color.X, restored.X, 2);
        Assert.Equal(color.Y, restored.Y, 2);
        Assert.Equal(color.Z, restored.Z, 2);
        Assert.Equal(1f, restored.W, 2);
        Assert.Equal(0u, ChannelInk.Pack(new Vector4(0f, 0f, 0f, 0f)));
    }

    [Fact]
    public void InkSlotsReadAndWriteTheirOwnField()
    {
        var style = new ChannelStyle();
        for (var slot = 0; slot < ChannelStyle.InkSlotCount; slot++)
        {
            style.SetInk(slot, (uint)(0xFF000001u + (uint)slot));
        }

        Assert.Equal(0xFF000001u, style.IncomingName);
        Assert.Equal(0xFF000002u, style.IncomingBody);
        Assert.Equal(0xFF000003u, style.OutgoingName);
        Assert.Equal(0xFF000004u, style.OutgoingBody);
        for (var slot = 0; slot < ChannelStyle.InkSlotCount; slot++)
        {
            Assert.Equal((uint)(0xFF000001u + (uint)slot), style.Ink(slot));
        }

        style.Clear();
        Assert.True(style.IsDefault);
    }
}
