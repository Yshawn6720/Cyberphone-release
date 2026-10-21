using System.Text;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SoftWrapMatchTests
{
    [Theory]
    [InlineData("")]
    [InlineData("why?")]
    [InlineData("为什么要这样做")]
    [InlineData("mixed 混合 text 😀 with emoji")]
    [InlineData("line one\nline two")]
    public void ATextMatchesItsOwnEncoding(string text)
    {
        Assert.True(SoftWrap.MatchesUtf8(text, Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void ATextLongerThanOneCompareChunkStillMatches()
    {
        var text = new string('字', 700);
        Assert.True(SoftWrap.MatchesUtf8(text, Encoding.UTF8.GetBytes(text)));
        Assert.False(SoftWrap.MatchesUtf8(text, Encoding.UTF8.GetBytes(text[..699] + '句')));
    }

    [Theory]
    [InlineData("why", "why?")]
    [InlineData("why?", "why")]
    [InlineData("", "为")]
    [InlineData("为", "")]
    [InlineData("为什么", "为什麼")]
    [InlineData("a😀", "a😁")]
    public void DifferentTextsNeverMatch(string text, string other)
    {
        Assert.False(SoftWrap.MatchesUtf8(text, Encoding.UTF8.GetBytes(other)));
    }
}
