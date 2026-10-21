using System.Globalization;
using Aetherphone.Apps.Calculator;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CalculatorTextTests
{
    private static readonly NumberMarks German = new(',', '.');
    private static readonly NumberMarks French = new(',', ' ');

    [Theory]
    [InlineData("0", "0")]
    [InlineData("999", "999")]
    [InlineData("1000", "1,000")]
    [InlineData("1234567.891", "1,234,567.891")]
    [InlineData("-1234", "-1,234")]
    [InlineData("0.", "0.")]
    [InlineData("1.5e18", "1.5e18")]
    [InlineData("2.5e-12", "2.5e-12")]
    [InlineData("1234 × 5678 =", "1,234 × 5,678 =")]
    public void LocalizeGroupsEveryNumber(string raw, string expected)
    {
        Assert.Equal(expected, CalculatorText.Localize(raw, NumberMarks.Invariant));
    }

    [Fact]
    public void LocalizeUsesTheLanguageMarks()
    {
        Assert.Equal("1.234.567,5", CalculatorText.Localize("1234567.5", German));
        Assert.Equal("12 000 + 0,25", CalculatorText.Localize("12000 + 0.25", French));
    }

    [Fact]
    public void MarksNormalizeSpacingGroupSeparators()
    {
        var marks = NumberMarks.From(CultureInfo.GetCultureInfo("fr-FR").NumberFormat);

        Assert.Equal(',', marks.Decimal);
        Assert.Equal(' ', marks.Group);
    }

    [Fact]
    public void MarksNeverUseOneCharacterForBoth()
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.NumberGroupSeparator = ".";
        format.NumberDecimalSeparator = ".";

        var marks = NumberMarks.From(format);

        Assert.NotEqual(marks.Decimal, marks.Group);
    }

    [Fact]
    public void ClipboardTextHasNoGroupingAndTheLocalDecimal()
    {
        Assert.Equal("1234567.5", CalculatorText.ForClipboard("1234567.5", NumberMarks.Invariant));
        Assert.Equal("1234567,5", CalculatorText.ForClipboard("1234567.5", German));
    }

    [Theory]
    [InlineData("1,234,567", 1234567.0)]
    [InlineData("1234.5", 1234.5)]
    [InlineData(" -42 ", -42.0)]
    [InlineData("+7", 7.0)]
    [InlineData("1 000 000", 1000000.0)]
    [InlineData("1'000", 1000.0)]
    [InlineData("1e3", 1000.0)]
    [InlineData("2.5E-2", 0.025)]
    public void PasteReadsInvariantNumbers(string text, double expected)
    {
        Assert.True(CalculatorText.TryParsePasted(text, NumberMarks.Invariant, out var value));
        Assert.Equal(expected, value, 9);
    }

    [Theory]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1.234.567", 1234567.0)]
    [InlineData("1234,5", 1234.5)]
    [InlineData("1234.5", 1234.5)]
    public void PasteReadsGermanNumbers(string text, double expected)
    {
        Assert.True(CalculatorText.TryParsePasted(text, German, out var value));
        Assert.Equal(expected, value, 9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12 gil")]
    [InlineData("1.2.3")]
    [InlineData("-")]
    [InlineData("1.5,5")]
    public void PasteRejectsAnythingElse(string? text)
    {
        Assert.False(CalculatorText.TryParsePasted(text, NumberMarks.Invariant, out _));
    }

    [Fact]
    public void PastedClipboardRoundTripsInEveryLanguage()
    {
        string[] cultures = { "de-DE", "en-US", "es-ES", "fr-FR", "ja-JP", "pt-BR", "ru-RU", "tr-TR", "zh-CN" };
        for (var index = 0; index < cultures.Length; index++)
        {
            var marks = NumberMarks.From(CultureInfo.GetCultureInfo(cultures[index]).NumberFormat);
            var clipboard = CalculatorText.ForClipboard("98765.4321", marks);

            Assert.True(CalculatorText.TryParsePasted(clipboard, marks, out var value), cultures[index]);
            Assert.Equal(98765.4321, value, 9);
        }
    }
}
