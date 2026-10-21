using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MarqueeIdTests
{
    [Fact]
    public void SamePartsAreEqualAndShareAHash()
    {
        var first = new MarqueeId("chirper.post.", "abc123");
        var second = new MarqueeId("chirper.post.", "abc123");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ADifferentPrefixOrSuffixIsADifferentId()
    {
        var baseline = new MarqueeId("chirper.post.", "abc123");

        Assert.NotEqual(baseline, new MarqueeId("chirper.reply.", "abc123"));
        Assert.NotEqual(baseline, new MarqueeId("chirper.post.", "abc124"));
    }

    [Fact]
    public void TheTwoPartFormNeverCollidesAcrossASplitBoundary()
    {
        Assert.NotEqual(new MarqueeId("row.", "1.title"), new MarqueeId("row.1.", "title"));
    }

    [Fact]
    public void AStringConvertsToTheSameIdEveryTime()
    {
        MarqueeId fromString = "settings.language";
        MarqueeId again = "settings.language";

        Assert.Equal(fromString, again);
        Assert.Equal(fromString, new MarqueeId("settings.language", string.Empty));
    }

    [Fact]
    public void OrdinalIdsCompareOnTheirNumber()
    {
        var first = new MarqueeId("collections.tile.", 3L);

        Assert.Equal(first, new MarqueeId("collections.tile.", 3L));
        Assert.NotEqual(first, new MarqueeId("collections.tile.", 4L));
        Assert.NotEqual(first, new MarqueeId("collections.grid.", 3L));
    }

    [Fact]
    public void AnOrdinalIdIsDistinctFromTheTextThatLooksLikeIt()
    {
        Assert.NotEqual(new MarqueeId("tile.", 3L), new MarqueeId("tile.", "3"));
    }

    [Fact]
    public void WorksAsADictionaryKey()
    {
        var map = new Dictionary<MarqueeId, float>
        {
            [new MarqueeId("a.", "one")] = 1f,
            [new MarqueeId("a.", "two")] = 2f,
            [new MarqueeId("b.", "one")] = 3f,
            [new MarqueeId("a.", 1L)] = 4f,
            ["plain"] = 5f,
        };

        Assert.Equal(5, map.Count);
        Assert.Equal(1f, map[new MarqueeId("a.", "one")]);
        Assert.Equal(3f, map[new MarqueeId("b.", "one")]);
        Assert.Equal(4f, map[new MarqueeId("a.", 1L)]);
        Assert.Equal(5f, map["plain"]);

        map[new MarqueeId("a.", "one")] = 9f;
        Assert.Equal(5, map.Count);
        Assert.Equal(9f, map[new MarqueeId("a.", "one")]);
    }
}
