using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Report;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ReportCategoryTests
{
    private static readonly string[] ServerIds =
    {
        "spam", "harassment", "hate", "threats", "impersonation", "explicit", "ingame", "creators", "privacy",
        "consent", "selfharm", "child", "exploitation", "evasion", "other",
    };

    private static string[] IdsFor(ReportVenue venue)
    {
        Span<int> visible = stackalloc int[ReportCategories.All.Length];
        var count = ReportCategories.Collect(venue, visible);
        var ids = new string[count];
        for (var index = 0; index < count; index++)
        {
            ids[index] = ReportCategories.All[visible[index]].Id;
        }

        return ids;
    }

    [Fact]
    public void EveryCategoryUsesAnIdTheServerKnows()
    {
        for (var index = 0; index < ReportCategories.All.Length; index++)
        {
            Assert.Contains(ReportCategories.All[index].Id, ServerIds);
        }
    }

    [Theory]
    [InlineData((byte)ReportVenue.General)]
    [InlineData((byte)ReportVenue.Social)]
    [InlineData((byte)ReportVenue.Velvet)]
    public void EachVenueListsEveryIdOnceAndEndsWithSomethingElse(byte venue)
    {
        var ids = IdsFor((ReportVenue)venue);

        Assert.Equal(ids.Length, new HashSet<string>(ids).Count);
        Assert.Equal("other", ids[^1]);
        Assert.Contains("child", ids);
        Assert.Contains("exploitation", ids);
    }

    [Fact]
    public void AppSpecificRulesOnlyShowWhereTheyApply()
    {
        var general = IdsFor(ReportVenue.General);
        var social = IdsFor(ReportVenue.Social);
        var velvet = IdsFor(ReportVenue.Velvet);

        Assert.DoesNotContain("ingame", general);
        Assert.DoesNotContain("consent", general);
        Assert.Contains("ingame", social);
        Assert.DoesNotContain("consent", social);
        Assert.Contains("consent", velvet);
        Assert.DoesNotContain("ingame", velvet);
        Assert.Equal(13, general.Length);
        Assert.Equal(14, social.Length);
        Assert.Equal(14, velvet.Length);
    }

    [Fact]
    public void VelvetRelabelsTheExplicitCategory()
    {
        Span<int> visible = stackalloc int[ReportCategories.All.Length];
        var count = ReportCategories.Collect(ReportVenue.Velvet, visible);
        for (var index = 0; index < count; index++)
        {
            var entry = ReportCategories.All[visible[index]];
            if (entry.Id == "explicit")
            {
                Assert.Equal("report.categoryVelvetProhibited", entry.Label.Key);
                return;
            }
        }

        Assert.Fail("Velvet has no explicit category");
    }

    [Fact]
    public void RequestSendsTheCategoryNextToTheDetails()
    {
        var request = new ReportRequest("post", "p1", "spammy link", null, "spam");

        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.ReportRequest);

        Assert.Contains("\"category\":\"spam\"", json);
        Assert.Contains("\"reason\":\"spammy link\"", json);
    }
}
