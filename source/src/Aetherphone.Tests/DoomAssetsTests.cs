using Aetherphone.Apps.Games.Doom;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DoomAssetsTests
{
    [Fact]
    public void AFullGamePrefersItsOwnDataOverTheSharewareEpisode()
    {
        var files = new[] { "doom1.wad", "readme.txt", "DOOM.WAD" };
        Assert.Equal("DOOM.WAD", DoomAssets.PreferredIwad(files));
    }

    [Fact]
    public void DoomTwoOutranksDoomOne()
    {
        var files = new[] { "doom.wad", "doom2.wad", "doom1.wad" };
        Assert.Equal("doom2.wad", DoomAssets.PreferredIwad(files));
    }

    [Fact]
    public void NothingIsPickedWhenNoKnownDataIsPresent()
    {
        var files = new[] { "notes.txt", "TimGM6mb.sf2" };
        Assert.Null(DoomAssets.PreferredIwad(files));
    }
}
