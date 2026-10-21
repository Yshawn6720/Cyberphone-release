using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LocalMediaTokenTests
{
    private const string SampleHash = "0f9c2d3e4a5b6c7d8e9f0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f";

    [Theory]
    [InlineData("movie night.mp4")]
    [InlineData("weird:name:with:colons.mkv")]
    [InlineData("日本語の動画.webm")]
    [InlineData("100% legit & special #chars.mov")]
    public void FormatRoundTripsThroughParse(string fileName)
    {
        var token = LocalMediaToken.Format(fileName, 123456789L, SampleHash);

        Assert.True(LocalMediaToken.IsToken(token));
        Assert.True(LocalMediaToken.TryParse(token, out var identity));
        Assert.Equal(fileName, identity.FileName);
        Assert.Equal(123456789L, identity.SizeBytes);
        Assert.Equal(SampleHash, identity.ContentHash);
        Assert.Equal(token, identity.Token);
    }

    [Fact]
    public void FormatBoundsOverlongFileNames()
    {
        var fileName = new string('a', 400) + ".mp4";
        var token = LocalMediaToken.Format(fileName, 42L, SampleHash);

        Assert.True(LocalMediaToken.TryParse(token, out var identity));
        Assert.Equal(180, identity.FileName.Length);
        Assert.True(token.Length < 2048);
    }

    [Theory]
    [InlineData("https://example.com/video.mp4")]
    [InlineData("C:\\Videos\\movie.mp4")]
    [InlineData("aep-local:2:" + SampleHash + ":100:name")]
    [InlineData("aep-local:1:tooshort:100:name")]
    [InlineData("aep-local:1:" + SampleHash + ":notanumber:name")]
    [InlineData("aep-local:1:" + SampleHash + ":-5:name")]
    [InlineData("aep-local:1:" + SampleHash + ":0:name")]
    [InlineData("aep-local:1:" + SampleHash + ":100:")]
    [InlineData("aep-local:1:" + SampleHash + ":100")]
    [InlineData("aep-local:")]
    public void ParseRejectsMalformedTokens(string url)
    {
        Assert.False(LocalMediaToken.TryParse(url, out _));
    }

    [Fact]
    public void ParseRejectsUppercaseHash()
    {
        var token = "aep-local:1:" + SampleHash.ToUpperInvariant() + ":100:name";

        Assert.False(LocalMediaToken.TryParse(token, out _));
    }

    [Fact]
    public void ComputeIsStableAndSensitiveToContent()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".mp4");
        try
        {
            var payload = new byte[600 * 1024];
            for (var index = 0; index < payload.Length; index++)
            {
                payload[index] = (byte)(index * 31);
            }

            File.WriteAllBytes(path, payload);
            var first = LocalMediaToken.TryCompute(path);
            var second = LocalMediaToken.TryCompute(path);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(payload.LongLength, first.SizeBytes);
            Assert.Equal(Path.GetFileName(path), first.FileName);
            Assert.Equal(first.ContentHash, second.ContentHash);
            Assert.True(first.Matches(second));

            payload[0] ^= 0xFF;
            File.WriteAllBytes(path, payload);
            var changedHead = LocalMediaToken.TryCompute(path);
            Assert.NotNull(changedHead);
            Assert.NotEqual(first.ContentHash, changedHead.ContentHash);

            payload[^1] ^= 0xFF;
            File.WriteAllBytes(path, payload);
            var changedTail = LocalMediaToken.TryCompute(path);
            Assert.NotNull(changedTail);
            Assert.NotEqual(changedHead.ContentHash, changedTail.ContentHash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ComputeHandlesFilesSmallerThanTheSampleWindow()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".mp4");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
            var identity = LocalMediaToken.TryCompute(path);

            Assert.NotNull(identity);
            Assert.Equal(5L, identity.SizeBytes);
            Assert.True(LocalMediaToken.TryParse(identity.Token, out var parsed));
            Assert.True(identity.Matches(parsed));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ComputeReturnsNullForMissingFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".mp4");

        Assert.Null(LocalMediaToken.TryCompute(path));
    }

    [Fact]
    public void MatchesComparesHashAndSizeOnly()
    {
        var left = new LocalMediaIdentity("a.mp4", 100L, SampleHash);
        var sameContent = new LocalMediaIdentity("renamed.mp4", 100L, SampleHash);
        var differentSize = new LocalMediaIdentity("a.mp4", 101L, SampleHash);

        Assert.True(left.Matches(sameContent));
        Assert.False(left.Matches(differentSize));
    }
}
