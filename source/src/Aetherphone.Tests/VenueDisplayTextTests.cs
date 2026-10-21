using Aetherphone.Core.Venues;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueDisplayTextTests
{
    [Fact]
    public void Clean_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, VenueDisplayText.Clean(null));
        Assert.Equal(string.Empty, VenueDisplayText.Clean(string.Empty));
    }

    [Fact]
    public void Clean_PlainText_ReturnsSameInstance()
    {
        const string title = "Weekend Reset @ Glimmerglade - W9 P35";
        Assert.Same(title, VenueDisplayText.Clean(title));
    }

    [Fact]
    public void Clean_DecoratedStreamTitle_CollapsesSymbolsIntoSeparators()
    {
        var raw = "⟡ Weekend Reset @ Glimmerglade ◦ ∙ ✦ ∙ ◦ Dynamis ⋆ Marilith ⋆ Goblet ⋆ W9 P35 ♡ UP NEXT: @noasaitoshii";
        Assert.Equal("Weekend Reset @ Glimmerglade · Dynamis · Marilith · Goblet · W9 P35 · UP NEXT: @noasaitoshii",
            VenueDisplayText.Clean(raw));
    }

    [Fact]
    public void Clean_Emoji_AreDroppedWithoutLeavingDoubleSpaces()
    {
        Assert.Equal("Come dance tonight", VenueDisplayText.Clean("Come dance 💃🏽 tonight"));
        Assert.Equal("Live", VenueDisplayText.Clean("🎵 Live 🎵"));
        Assert.Equal("Heart", VenueDisplayText.Clean("Heart❤️"));
    }

    [Fact]
    public void Clean_MathematicalAlphanumerics_MapToAscii()
    {
        var bold = char.ConvertFromUtf32(0x1D400) + char.ConvertFromUtf32(0x1D41B);
        Assert.Equal("Ab club", VenueDisplayText.Clean(bold + " club"));
    }

    [Fact]
    public void Clean_KeepsAccentedLatinCyrillicKanaAndIdeographs()
    {
        Assert.Same("Café Élan", VenueDisplayText.Clean("Café Élan"));
        Assert.Equal("Бар · ラウンジ 夜", VenueDisplayText.Clean("Бар ✦ ラウンジ 夜"));
    }

    [Fact]
    public void Clean_WhitespaceRunsAndEdgesAreTrimmed()
    {
        Assert.Equal("Open bar", VenueDisplayText.Clean("  Open \t\n bar  "));
        Assert.Equal("A · B", VenueDisplayText.Clean("A · · B"));
        Assert.Equal(string.Empty, VenueDisplayText.Clean("✦ ◦ ✦"));
    }

    [Fact]
    public void Clean_InvisibleFormattingIsRemoved()
    {
        Assert.Equal("Glimmer", VenueDisplayText.Clean("Glim\u200Bmer\uFE0F"));
    }

    [Fact]
    public void Clean_UnsupportedScriptsBecomeSpaces()
    {
        Assert.Equal("Club night", VenueDisplayText.Clean("Club ไทย night"));
    }

    [Fact]
    public void Clean_SmallCapsAndCircledLettersMapToAscii()
    {
        Assert.Equal("THE BAR", VenueDisplayText.Clean("\u1D1B\u029C\u1D07 \u0299\u1D00\u0280"));
        Assert.Equal("The Bar", VenueDisplayText.Clean("The \u24B7\u24D0\u24E1"));
    }

    [Fact]
    public void Clean_BidiAndLineSeparatorControlsAreRemoved()
    {
        Assert.Equal("Moonlit", VenueDisplayText.Clean("\u202AMoon\u2028lit\u202C"));
    }
}
