using Aetherphone.Apps.Camera;
using Aetherphone.Core.Photos;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhotoLibraryLogicTests
{
    [Fact]
    public void RunsSplitConsecutiveKeys()
    {
        var runs = new List<PhotoRun>();
        PhotoGrouping.Runs(new[] { 202610, 202610, 202609, 202512, 202512, 202512 }, runs);

        Assert.Equal(3, runs.Count);
        Assert.Equal(new PhotoRun(202610, 0, 2), runs[0]);
        Assert.Equal(new PhotoRun(202609, 2, 1), runs[1]);
        Assert.Equal(new PhotoRun(202512, 3, 3), runs[2]);
    }

    [Fact]
    public void RunsOfNothingAreEmpty()
    {
        var runs = new List<PhotoRun> { new(1, 0, 1) };
        PhotoGrouping.Runs(ReadOnlySpan<int>.Empty, runs);

        Assert.Empty(runs);
    }

    [Fact]
    public void MonthKeyRoundTripsToItsYear()
    {
        var key = PhotoGrouping.MonthKey(new DateTime(2026, 10, 3, 14, 5, 0));

        Assert.Equal(202610, key);
        Assert.Equal(2026, PhotoGrouping.YearOfMonthKey(key));
    }

    [Fact]
    public void BucketsKeepFirstAppearanceOrderAndSkipUnknownPlaces()
    {
        var buckets = PhotoGrouping.Buckets(new uint[] { 0, 128, 132, 128, 0, 132, 129 });

        Assert.Equal(3, buckets.Length);
        Assert.Equal(128u, buckets[0].Key);
        Assert.Equal(new[] { 1, 3 }, buckets[0].Indices);
        Assert.Equal(132u, buckets[1].Key);
        Assert.Equal(new[] { 2, 5 }, buckets[1].Indices);
        Assert.Equal(129u, buckets[2].Key);
    }

    [Fact]
    public void PlacesAreKeyedByFileNameSoMovesKeepThem()
    {
        var places = new Dictionary<string, uint>();

        Assert.True(PhotoPlaces.Stamp(places, @"C:\config\Photos\AEP_20261003_140500_000.png", 128));
        Assert.Equal(128u, PhotoPlaces.TerritoryOf(places, @"C:\config\Photos\.trash\AEP_20261003_140500_000.png"));
    }

    [Fact]
    public void StampingAnUnknownTerritoryIsIgnored()
    {
        var places = new Dictionary<string, uint>();

        Assert.False(PhotoPlaces.Stamp(places, "AEP_20261003_140500_000.png", 0));
        Assert.Empty(places);
    }

    [Fact]
    public void EditedCopiesInheritTheSourcePlace()
    {
        var places = new Dictionary<string, uint> { ["AEP_1.png"] = 132 };

        Assert.True(PhotoPlaces.CopyStamp(places, @"C:\p\AEP_1.png", @"C:\p\AEP_2.png"));
        Assert.Equal(132u, places["AEP_2.png"]);
        Assert.False(PhotoPlaces.CopyStamp(places, @"C:\p\AEP_9.png", @"C:\p\AEP_10.png"));
    }

    [Fact]
    public void PruneDropsPlacesOfPhotosThatAreGone()
    {
        var places = new Dictionary<string, uint> { ["kept.png"] = 1, ["gone.png"] = 2 };

        Assert.True(PhotoPlaces.Prune(places, new HashSet<string> { "kept.png" }));
        Assert.Single(places);
        Assert.False(PhotoPlaces.Prune(places, new HashSet<string> { "kept.png" }));
    }

    [Fact]
    public void DownsampleAveragesBlocks()
    {
        var pixels = new byte[4 * 4 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 200;
            pixels[index + 3] = 255;
        }

        pixels[0] = 0;
        var image = PhotoPixels.Downsample(pixels, 4, 4, 2);

        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(150, image.Pixels[0]);
        Assert.Equal(200, image.Pixels[4]);
        Assert.Equal(255, image.Pixels[3]);
    }

    [Fact]
    public void DownsampleKeepsSmallImagesUntouched()
    {
        var pixels = new byte[2 * 2 * 4];
        var image = PhotoPixels.Downsample(pixels, 2, 2, 256);

        Assert.Same(pixels, image.Pixels);
    }

    [Fact]
    public void DownsampleRejectsShortBuffers()
    {
        var image = PhotoPixels.Downsample(new byte[3], 2, 2, 1);

        Assert.Equal(0, image.Width);
    }

    [Fact]
    public void ShortDragsSnapBack()
    {
        Assert.Equal(0, PhotoPaging.Settle(-40f, 0f, 360f, true, true, 1f));
    }

    [Fact]
    public void FarDragsTurnThePage()
    {
        Assert.Equal(1, PhotoPaging.Settle(-120f, 0f, 360f, true, true, 1f));
        Assert.Equal(-1, PhotoPaging.Settle(120f, 0f, 360f, true, true, 1f));
    }

    [Fact]
    public void FlingsTurnThePageEvenWhenShort()
    {
        Assert.Equal(1, PhotoPaging.Settle(-20f, -900f, 360f, true, true, 1f));
    }

    [Fact]
    public void EdgesNeverTurnAndResist()
    {
        Assert.Equal(0, PhotoPaging.Settle(-200f, -900f, 360f, true, false, 1f));
        Assert.Equal(0, PhotoPaging.Settle(200f, 900f, 360f, false, true, 1f));
        Assert.Equal(100f * PhotoPaging.EdgeResistance, PhotoPaging.Resist(100f, false, true));
        Assert.Equal(-100f, PhotoPaging.Resist(-100f, false, true));
    }

    [Fact]
    public void TimerCyclesOffThreeTen()
    {
        Assert.Equal(3, CameraApp.NextTimer(0));
        Assert.Equal(10, CameraApp.NextTimer(3));
        Assert.Equal(0, CameraApp.NextTimer(10));
        Assert.Equal(0, CameraApp.NextTimer(7));
    }
}
