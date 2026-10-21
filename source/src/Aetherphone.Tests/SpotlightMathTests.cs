using Aetherphone.Core.Shell.Spotlight;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SpotlightMathTests
{
    [Theory]
    [InlineData("1200*3", "3600")]
    [InlineData("12 + 4", "16")]
    [InlineData("(2+3)*4", "20")]
    [InlineData("10-2-3", "5")]
    [InlineData("2+3*4", "14")]
    [InlineData("7/2", "3.5")]
    [InlineData("12x4", "48")]
    [InlineData("10%3", "1")]
    [InlineData("-5+8", "3")]
    [InlineData("1.5*2", "3")]
    public void Evaluates_Arithmetic(string input, string expected)
    {
        Assert.True(SpotlightMath.TryEvaluate(input, out var formatted));
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("hello")]
    [InlineData("map + zone")]
    [InlineData("1/0")]
    [InlineData("(2+3")]
    [InlineData("2+")]
    [InlineData("+")]
    [InlineData("2 3")]
    public void Rejects_NonExpressions(string input)
    {
        Assert.False(SpotlightMath.TryEvaluate(input, out _));
    }
}
