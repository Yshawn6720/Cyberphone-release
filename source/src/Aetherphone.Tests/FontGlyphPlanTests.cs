using System.Reflection;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FontGlyphPlanTests
{
    [Fact]
    public void EveryLocalizedGlyphIsCoveredByTheNativeOrSharedPlan()
    {
        var directory = LocalizationDirectory();
        for (var index = 0; index < Languages.All.Length; index++)
        {
            var language = Languages.All[index];
            var glyphs = StringCatalog.ScanGlyphs(Path.Combine(directory, string.Concat(language.Code, ".json")));
            Assert.True(glyphs.Length > 0, $"'{language.Code}.json' is missing, empty, or failed to scan.");

            var native = new GlyphCoverage();
            native.AddRanges(GlyphPlan.Native(language));

            var planned = Plan(native, glyphs);
            var shared = new GlyphCoverage();
            shared.AddRanges(planned.ToRanges(GlyphPlan.FirstSharedCodepoint));
            for (var glyphIndex = 0; glyphIndex < glyphs.Length; glyphIndex++)
            {
                var codepoint = glyphs[glyphIndex];
                Assert.True(native.Contains(codepoint) || shared.Contains(codepoint),
                    $"'{language.Code}.json' uses U+{codepoint:X4} but no font covers it.");
            }
        }
    }

    [Fact]
    public void ChineseRoutesItsIdeographsThroughTheSharedRaster()
    {
        var directory = LocalizationDirectory();
        var glyphs = StringCatalog.ScanGlyphs(Path.Combine(directory, "zh.json"));
        var native = new GlyphCoverage();
        native.AddRanges(GlyphPlan.Native(Languages.Chinese));

        var pickerLabels = NativeNameCodepoints();
        var ideographs = 0;
        for (var index = 0; index < glyphs.Length; index++)
        {
            var codepoint = glyphs[index];
            if (codepoint is < 0x3400 or > 0x9FFF)
            {
                continue;
            }

            ideographs++;
            if (pickerLabels.Contains(codepoint))
            {
                continue;
            }

            Assert.False(native.Contains(codepoint),
                $"U+{codepoint:X4} is baked into all 48 text buckets instead of the shared raster.");
        }

        Assert.True(ideographs > 1000, $"Expected the Chinese catalog to need many ideographs, found {ideographs}.");
    }

    [Fact]
    public void EnglishCoversTheNonLatinGlyphsItsOwnStringsUse()
    {
        var glyphs = StringCatalog.ScanGlyphs(Path.Combine(LocalizationDirectory(), "en.json"));
        var native = new GlyphCoverage();
        native.AddRanges(GlyphPlan.Native(Languages.English));

        var shared = new GlyphCoverage();
        shared.AddRanges(Plan(native, glyphs).ToRanges(GlyphPlan.FirstSharedCodepoint));

        int[] required = { 0x2922, 0x77F3, 0x4E4B, 0x5BB6 };
        for (var index = 0; index < required.Length; index++)
        {
            var codepoint = required[index];
            Assert.True(native.Contains(codepoint) || shared.Contains(codepoint),
                $"English uses U+{codepoint:X4} but no font covers it.");
        }
    }

    [Fact]
    public void TheSharedBaseCoversTheScriptsRemoteTextArrivesIn()
    {
        const string samples = "ПриветЎўΑΩθήναいろはにカタカナ、。「」０９ＡＺｦﾝ№←→ǍẞảỹЀ€₽̈";
        for (var index = 0; index < samples.Length; index++)
        {
            int codepoint = samples[index];
            Assert.True(GlyphPlan.IsSharedBase(codepoint),
                $"U+{codepoint:X4} still has to be learned at runtime, which rebuilds the whole font atlas.");
        }
    }

    [Fact]
    public void SharedBaseMembershipMatchesItsRanges()
    {
        var coverage = new GlyphCoverage();
        coverage.AddRanges(GlyphPlan.SharedBase);

        for (var codepoint = 0; codepoint <= char.MaxValue; codepoint++)
        {
            Assert.Equal(coverage.Contains(codepoint), GlyphPlan.IsSharedBase(codepoint));
        }
    }

    [Fact]
    public void IdeographsStayOnTheLearnedLedgerInsteadOfTheSharedBase()
    {
        int[] ideographs = { 0x4E00, 0x5148, 0x751F, 0x9FFF, 0xAC00, 0xD7A3 };
        for (var index = 0; index < ideographs.Length; index++)
        {
            Assert.False(GlyphPlan.IsSharedBase(ideographs[index]),
                $"U+{ideographs[index]:X4} would rasterize a block far too large for the shared font.");
        }
    }

    [Fact]
    public void ExcludedRangesAreSkippedWhenComposingCoverage()
    {
        var excluded = new GlyphCoverage();
        excluded.Add(0x0402);

        var coverage = new GlyphCoverage();
        coverage.AddRanges(new ushort[] { 0x0400, 0x0404 }, excluded);

        Assert.Equal(4, coverage.Count);
        Assert.True(coverage.Contains(0x0401));
        Assert.False(coverage.Contains(0x0402));
        Assert.True(coverage.Contains(0x0403));
    }

    [Fact]
    public void RunCompressionRoundTripsThroughRanges()
    {
        var coverage = new GlyphCoverage();
        int[] codepoints = { 0x0100, 0x0101, 0x0102, 0x4E00, 0x9FFF, char.MaxValue };
        for (var index = 0; index < codepoints.Length; index++)
        {
            Assert.True(coverage.Add(codepoints[index]));
            Assert.False(coverage.Add(codepoints[index]));
        }

        Assert.Equal(codepoints.Length, coverage.Count);

        var rebuilt = new GlyphCoverage();
        rebuilt.AddRanges(coverage.ToRanges(GlyphPlan.FirstSharedCodepoint));

        Assert.Equal(coverage.Count, rebuilt.Count);
        for (var index = 0; index < codepoints.Length; index++)
        {
            Assert.True(rebuilt.Contains(codepoints[index]), $"U+{codepoints[index]:X4} was lost by run compression.");
        }

        Assert.False(rebuilt.Contains(0x0103));
        Assert.False(rebuilt.Contains(0x4E01));
    }

    [Fact]
    public void EmptyCoverageProducesATerminatorOnlyRange()
    {
        var coverage = new GlyphCoverage();
        var ranges = coverage.ToRanges(GlyphPlan.FirstSharedCodepoint);

        Assert.Single(ranges);
        Assert.Equal(0, ranges[0]);
    }

    private static HashSet<int> NativeNameCodepoints()
    {
        var codepoints = new HashSet<int>();
        for (var index = 0; index < Languages.All.Length; index++)
        {
            var name = Languages.All[index].NativeName;
            for (var charIndex = 0; charIndex < name.Length; charIndex++)
            {
                codepoints.Add(name[charIndex]);
            }
        }

        return codepoints;
    }

    private static GlyphCoverage Plan(GlyphCoverage native, ushort[] glyphs)
    {
        var planned = new GlyphCoverage();
        for (var index = 0; index < glyphs.Length; index++)
        {
            var codepoint = glyphs[index];
            if (native.Contains(codepoint))
            {
                continue;
            }

            planned.Add(codepoint);
        }

        return planned;
    }

    private static string LocalizationDirectory()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "src", "Aetherphone", "Localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory)!;
        }

        throw new DirectoryNotFoundException("Could not locate src/Aetherphone/Localization from the test assembly.");
    }
}
