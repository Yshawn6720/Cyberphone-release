using System.Numerics;
using Aetherphone.Core.GameChat;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChatRunsTests
{
    private static ChatEntry Entry(string id, params ChatChunk[] chunks)
    {
        var text = string.Concat(Array.ConvertAll(chunks, chunk => chunk.Text));
        return new ChatEntry(long.Parse(id), GameChannels.SayKey, "Rin", "Siren", text, chunks, DateTime.Now,
            ChatEntryFlags.None);
    }

    private static ChatRunSet Build(string id, params ChatChunk[] chunks)
    {
        ChatRuns.Reset();
        return ChatRuns.For(Entry(id, chunks));
    }

    [Fact]
    public void PlainTextProducesOneInertRun()
    {
        var set = Build("1", ChatChunk.Plain("just talking"));
        Assert.Single(set.Runs);
        Assert.False(set.Runs[0].Interactive);
        Assert.False(set.HasLinks);
        Assert.Empty(set.Targets);
    }

    [Fact]
    public void GameLinksBecomeInteractiveRunsWithTargets()
    {
        var set = Build("2", ChatChunk.Plain("wts "), ChatChunk.Item("Moonward Blade", 38951u),
            ChatChunk.Plain(" cheap"));
        Assert.Equal(3, set.Runs.Length);
        Assert.False(set.Runs[0].Interactive);
        Assert.True(set.Runs[1].Interactive);
        Assert.False(set.Runs[2].Interactive);
        Assert.True(set.HasLinks);
        Assert.Single(set.Targets);
        Assert.Equal(0, set.Runs[1].Target);
        Assert.Equal(38951u, set.Targets[0].Id);
    }

    [Theory]
    [InlineData("see https://xivapi.com now", "https://xivapi.com")]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("go to www.finalfantasyxiv.com ok", "www.finalfantasyxiv.com")]
    [InlineData("wrapped (https://example.com/page) here", "https://example.com/page")]
    [InlineData("trailing https://example.com.", "https://example.com")]
    public void UrlsAreDetectedInsidePlainText(string body, string expected)
    {
        var set = Build("3", ChatChunk.Plain(body));
        Assert.True(set.HasLinks);
        Assert.Single(set.Targets);
        Assert.Equal(ChatChunkKind.Url, set.Targets[0].Kind);
        Assert.Equal(expected, set.Targets[0].Text);
    }

    [Theory]
    [InlineData("no links here at all")]
    [InlineData("shhttp://notaurl")]
    [InlineData("mailto:someone@example.com")]
    public void NonUrlsStayPlain(string body)
    {
        var set = Build("4", ChatChunk.Plain(body));
        Assert.False(set.HasLinks);
        Assert.Empty(set.Targets);
    }

    [Fact]
    public void TwoUrlsInOneLineBothResolve()
    {
        var set = Build("5", ChatChunk.Plain("https://one.com and https://two.com"));
        Assert.Equal(2, set.Targets.Length);
        Assert.Equal("https://one.com", set.Targets[0].Text);
        Assert.Equal("https://two.com", set.Targets[1].Text);
    }

    [Fact]
    public void TargetIndexesStayAlignedAcrossMixedContent()
    {
        var set = Build("6", ChatChunk.Plain("meet at "), ChatChunk.Map("Yak T'el ( 24.1 , 8.3 )", 1187u, 942u, 10, 20),
            ChatChunk.Plain(" or see https://example.com"));
        Assert.Equal(2, set.Targets.Length);
        Assert.Equal(ChatChunkKind.Map, set.Targets[0].Kind);
        Assert.Equal(ChatChunkKind.Url, set.Targets[1].Kind);
        for (var index = 0; index < set.Runs.Length; index++)
        {
            var run = set.Runs[index];
            if (run.Interactive)
            {
                Assert.InRange(run.Target, 0, set.Targets.Length - 1);
                Assert.Equal(run.Text, set.Targets[run.Target].Text);
            }
        }
    }

    [Fact]
    public void AutoTranslateStaysReadableWithoutBecomingALink()
    {
        var set = Build("7", ChatChunk.AutoTranslate("Well met!"));
        Assert.Single(set.Runs);
        Assert.False(set.Runs[0].Interactive);
        Assert.Equal("Well met!", set.Runs[0].Text);
    }

    [Fact]
    public void EveryLinkKindGetsItsOwnTint()
    {
        var kinds = new[]
        {
            ChatChunkKind.Url, ChatChunkKind.Item, ChatChunkKind.Map, ChatChunkKind.Player,
            ChatChunkKind.Status, ChatChunkKind.Quest, ChatChunkKind.PartyFinder,
        };
        var seen = new HashSet<Vector4>();
        for (var index = 0; index < kinds.Length; index++)
        {
            Assert.True(seen.Add(ChatRuns.TintFor(kinds[index])), $"{kinds[index]} reused a tint");
        }
    }
}
