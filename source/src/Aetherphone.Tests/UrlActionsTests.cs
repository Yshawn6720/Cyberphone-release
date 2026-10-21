using Aetherphone.Windows;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UrlActionsTests
{
    private const string Ellipsis = "…";

    [Fact]
    public void ShortDestinationsStayWhole()
    {
        const string url = "https://example.com/path?query=1#frag";
        Assert.Equal(url, UrlActions.DestinationLabel(url));
    }

    [Fact]
    public void LongPathsAreCutAfterTheHost()
    {
        var url = "https://example.com/" + new string('a', 200);
        var label = UrlActions.DestinationLabel(url);
        Assert.StartsWith("https://example.com/", label, StringComparison.Ordinal);
        Assert.EndsWith(Ellipsis, label, StringComparison.Ordinal);
        Assert.Equal(72, label.Length);
    }

    [Fact]
    public void TheAuthorityIsNeverCut()
    {
        var authority = new string('h', 100) + ".example.com";
        var url = "https://" + authority + "/path/that/continues";
        var label = UrlActions.DestinationLabel(url);
        Assert.Equal("https://" + authority + Ellipsis, label);
    }

    [Fact]
    public void AnAuthorityOnlyUrlIsReturnedAsIs()
    {
        var url = "https://" + new string('h', 100) + ".example.com";
        Assert.Equal(url, UrlActions.DestinationLabel(url));
    }

    [Fact]
    public void UserInfoCannotHideTheRealHost()
    {
        var url = "https://trusted.example.com@" + new string('e', 80) + ".evil.example/" + new string('p', 50);
        var label = UrlActions.DestinationLabel(url);
        Assert.EndsWith(".evil.example" + Ellipsis, label, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemelessTextStillKeepsItsFirstSegmentWhole()
    {
        var url = new string('w', 90) + "/tail";
        var label = UrlActions.DestinationLabel(url);
        Assert.Equal(new string('w', 90) + Ellipsis, label);
    }
}
