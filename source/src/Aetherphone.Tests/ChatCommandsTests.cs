using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChatCommandsTests
{
    [Theory]
    [InlineData("/p heading out", "party", "heading out")]
    [InlineData("/party heading out", "party", "heading out")]
    [InlineData("/fc anyone up for maps?", "fc", "anyone up for maps?")]
    [InlineData("/s hello", "say", "hello")]
    [InlineData("/sh wts moonward", "shout", "wts moonward")]
    [InlineData("/y over here", "yell", "over here")]
    [InlineData("/l3 ty for the carry", "ls3", "ty for the carry")]
    [InlineData("/linkshell8 last one", "ls8", "last one")]
    [InlineData("/cwl1 savage sunday", "cwls1", "savage sunday")]
    [InlineData("/a pulling", "alliance", "pulling")]
    [InlineData("/e note to self", "echo", "note to self")]
    public void AbsorbsChannelCommands(string draft, string expectedKey, string expectedRemainder)
    {
        Assert.True(ChatCommands.TryAbsorb(draft, out var channel, out var remainder));
        Assert.Equal(expectedKey, channel.Key);
        Assert.Equal(expectedRemainder, remainder);
    }

    [Theory]
    [InlineData("/P HEADING OUT", "party")]
    [InlineData("/FC hello", "fc")]
    public void CommandsAreCaseInsensitive(string draft, string expectedKey)
    {
        Assert.True(ChatCommands.TryAbsorb(draft, out var channel, out _));
        Assert.Equal(expectedKey, channel.Key);
    }

    [Theory]
    [InlineData("hello there")]
    [InlineData("/p")]
    [InlineData("/notacommand hello")]
    [InlineData("/")]
    [InlineData("")]
    [InlineData("2/3 done")]
    public void LeavesEverythingElseAlone(string draft)
    {
        Assert.False(ChatCommands.TryAbsorb(draft, out _, out var remainder));
        Assert.Equal(draft, remainder);
    }

    [Fact]
    public void ReadOnlyChannelsHaveNoCommand()
    {
        Assert.False(ChatCommands.TryResolve("system", out _));
        Assert.True(ChatCommands.TryResolve("echo", out _));
    }

    [Fact]
    public void EverySendableChannelIsReachableByItsOwnCommand()
    {
        var all = GameChannels.All;
        for (var index = 0; index < all.Length; index++)
        {
            var channel = all[index];
            if (!channel.CanSend)
            {
                continue;
            }

            var token = channel.Command[1..];
            Assert.True(ChatCommands.TryResolve(token, out var resolved), $"{token} did not resolve");
            Assert.Equal(channel.Key, resolved.Key);
        }
    }
}
