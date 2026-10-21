using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChatChunkTests
{
    [Fact]
    public void PlainTextIsNeitherLinkNorTranslation()
    {
        var chunk = ChatChunk.Plain("hello");
        Assert.Equal(ChatChunkKind.Text, chunk.Kind);
        Assert.True(chunk.IsPlainText);
        Assert.False(chunk.IsLink);
    }

    [Fact]
    public void AutoTranslateCarriesTextWithoutBeingALink()
    {
        var chunk = ChatChunk.AutoTranslate("Well met!");
        Assert.Equal("Well met!", chunk.Text);
        Assert.False(chunk.IsPlainText);
        Assert.False(chunk.IsLink);
    }

    [Theory]
    [InlineData((byte)ChatChunkKind.Player)]
    [InlineData((byte)ChatChunkKind.Item)]
    [InlineData((byte)ChatChunkKind.Map)]
    [InlineData((byte)ChatChunkKind.Status)]
    [InlineData((byte)ChatChunkKind.Quest)]
    [InlineData((byte)ChatChunkKind.PartyFinder)]
    [InlineData((byte)ChatChunkKind.PluginLink)]
    public void EveryLinkKindReportsAsALink(byte rawKind)
    {
        var kind = (ChatChunkKind)rawKind;
        var chunk = kind switch
        {
            ChatChunkKind.Player => ChatChunk.Player("Aria", "Siren"),
            ChatChunkKind.Item => ChatChunk.Item("Moonward Blade", 38951u),
            ChatChunkKind.Map => ChatChunk.Map("Yak T'el ( 24.1 , 8.3 )", 1187u, 942u, 1024, 2048),
            ChatChunkKind.Status => ChatChunk.Status("Medicated", 49u),
            ChatChunkKind.Quest => ChatChunk.Quest("Endwalker", 70000u),
            ChatChunkKind.PartyFinder => ChatChunk.PartyFinder("Savage Sunday", 12345u),
            _ => ChatChunk.PluginLink("Open settings", "Aetherphone", 7u),
        };

        Assert.Equal(kind, chunk.Kind);
        Assert.True(chunk.IsLink);
        Assert.False(chunk.IsPlainText);
    }

    [Fact]
    public void LinkPayloadDataSurvivesOnTheChunk()
    {
        var item = ChatChunk.Item("Moonward Blade", 38951u);
        Assert.Equal(38951u, item.Id);

        var map = ChatChunk.Map("Yak T'el", 1187u, 942u, 1024, 2048);
        Assert.Equal(1187u, map.TerritoryId);
        Assert.Equal(942u, map.MapId);
        Assert.Equal(1024, map.RawX);
        Assert.Equal(2048, map.RawY);

        var player = ChatChunk.Player("Aria Solveig", "Siren");
        Assert.Equal("Siren", player.World);

        var plugin = ChatChunk.PluginLink("Open", "Aetherphone", 7u);
        Assert.Equal("Aetherphone", plugin.Plugin);
        Assert.Equal(7u, plugin.Id);
    }
}
