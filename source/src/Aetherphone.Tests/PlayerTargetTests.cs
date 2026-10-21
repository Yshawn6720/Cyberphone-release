using Aetherphone.Core.Game;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlayerTargetTests
{
    private const char CrossWorldGlyph = (char)0xE05D;
    private const char BellControlCharacter = (char)0x07;

    [Theory]
    [InlineData("Warrior Oflight", "Ravana", "Warrior Oflight@Ravana")]
    [InlineData("Y'shtola Rhul", "Odin", "Y'shtola Rhul@Odin")]
    [InlineData("Anne-Marie Bell", "Phantom", "Anne-Marie Bell@Phantom")]
    [InlineData("Warrior Oflight", "", "Warrior Oflight")]
    public void FormatsTheGameTarget(string name, string world, string expected)
    {
        Assert.True(PlayerTarget.TryFormat(name, world, out var target));
        Assert.Equal(expected, target);
    }

    [Fact]
    public void TakesTheWorldOffAnAlreadyQualifiedName()
    {
        Assert.True(PlayerTarget.TrySplit("Warrior Oflight@Ravana", string.Empty, out var name, out var world));
        Assert.Equal("Warrior Oflight", name);
        Assert.Equal("Ravana", world);
    }

    [Fact]
    public void ThePayloadWorldWinsOverTheOneInTheName()
    {
        Assert.True(PlayerTarget.TrySplit("Warrior Oflight@Ravana", "Odin", out var name, out var world));
        Assert.Equal("Warrior Oflight", name);
        Assert.Equal("Odin", world);
    }

    [Fact]
    public void TrimsPadding()
    {
        Assert.True(PlayerTarget.TrySplit("  Warrior Oflight  ", "Ravana", out var name, out _));
        Assert.Equal("Warrior Oflight", name);
    }

    [Fact]
    public void StripsTheGlyphsTheGamePrefixesNamesWith()
    {
        var raw = string.Concat(CrossWorldGlyph.ToString(), "Warrior", BellControlCharacter.ToString(), " Oflight");
        Assert.True(PlayerTarget.TrySplit(raw, "Ravana", out var name, out var world));
        Assert.Equal("Warrior Oflight", name);
        Assert.Equal("Ravana", world);
    }

    [Fact]
    public void KeepsNamesFromTheChineseClient()
    {
        Assert.True(PlayerTarget.TryFormat("光之战士", "神意之地", out var target));
        Assert.Equal("光之战士@神意之地", target);
    }

    [Theory]
    [InlineData("", "Ravana")]
    [InlineData("   ", "Ravana")]
    [InlineData("<t>", "Ravana")]
    [InlineData("Warrior Oflight /tell", "Ravana")]
    [InlineData("Warrior; Oflight", "Ravana")]
    [InlineData("Warrior Oflight who has a very long name indeed", "Ravana")]
    public void RefusesNamesTheGameCannotTake(string name, string world)
    {
        Assert.False(PlayerTarget.TryFormat(name, world, out var target));
        Assert.Equal(string.Empty, target);
    }

    [Theory]
    [InlineData("Warrior Oflight", "Ravana Prime")]
    [InlineData("Warrior Oflight", "Rav/ana")]
    [InlineData("Warrior Oflight", "Ravana2")]
    public void RefusesAWorldTheGameCannotTake(string name, string world)
    {
        Assert.False(PlayerTarget.TryFormat(name, world, out _));
    }
}
