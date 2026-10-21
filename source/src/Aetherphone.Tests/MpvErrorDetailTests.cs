using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MpvErrorDetailTests
{
    [Theory]
    [InlineData("ERROR: [twitch:stream] fatpat5: The channel is not currently live",
        "The channel is not currently live")]
    [InlineData("ERROR: [youtube] dQw4w9WgXcQ: Sign in to confirm you're not a bot. Use --cookies-from-browser or --cookies for the authentication. See https://github.com/yt-dlp/yt-dlp/wiki/FAQ",
        "Sign in to confirm you're not a bot")]
    [InlineData("ERROR: [youtube] abc: Video unavailable. This video has been removed by the uploader",
        "Video unavailable. This video has been removed by the uploader")]
    [InlineData("ERROR: [generic] x: Unable to download webpage: HTTP Error 403: Forbidden (caused by <HTTPError 403: Forbidden>); please report this issue on https://github.com/yt-dlp/yt-dlp/issues",
        "Unable to download webpage: HTTP Error 403: Forbidden")]
    [InlineData("ERROR: Unsupported URL: https://example.com/watch",
        "Unsupported URL: https://example.com/watch")]
    [InlineData("youtube-dl failed: not found or not enough permissions",
        "Not found or not enough permissions")]
    [InlineData("ERROR: [youtube] xyz: Playback on other websites has been disabled by the video owner",
        "Playback on other websites has been disabled by the video owner")]
    public void ResolverErrorsReadAsPlainReasons(string logged, string expected)
    {
        Assert.Equal(expected, MpvRenderer.CleanResolverError(logged));
    }
}
