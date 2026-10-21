using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlockedContentTests
{
    private const string Troll = "troll";
    private const string Friend = "friend";

    [Fact]
    public void ARechirpOfTheBlockedAuthorLeavesTheLane()
    {
        var chirp = Post("original", Troll, "venom");
        var lane = new[] { Post("mine", Friend, "hello"), Rechirp("share", Friend, chirp) };

        var purged = BlockedContent.Purge(lane, Troll);

        Assert.Single(purged);
        Assert.Equal("mine", purged[0].Id);
    }

    [Fact]
    public void AQuoteOfTheBlockedAuthorKeepsTheQuoterAndDropsTheQuotedChirp()
    {
        var chirp = Post("original", Troll, "venom");
        var quote = Post("quote", Friend, "look at this") with { QuotedPostId = chirp.Id, ReferencedPost = chirp };

        var purged = BlockedContent.Purge(new[] { quote }, Troll);

        var kept = Assert.Single(purged);
        Assert.Equal("look at this", kept.Text);
        Assert.Null(kept.ReferencedPost);
    }

    [Fact]
    public void PostsUntouchedByTheBlockKeepTheirArray()
    {
        var lane = new[] { Post("mine", Friend, "hello"), Rechirp("share", Friend, Post("other", "stranger", "hi")) };

        Assert.Same(lane, BlockedContent.Purge(lane, Troll));
    }

    [Fact]
    public void HidesCoversBothTheAuthorAndWhatTheyRechirped()
    {
        var chirp = Post("original", Troll, "venom");

        Assert.True(BlockedContent.Hides(chirp, Troll));
        Assert.True(BlockedContent.Hides(Rechirp("share", Friend, chirp), Troll));
        Assert.False(BlockedContent.Hides(Post("mine", Friend, "hello"), Troll));
    }

    private static PostDto Post(string id, string authorId, string text)
    {
        return new PostDto(id, authorId, string.Empty, string.Empty, string.Empty, authorId, text, 0,
            System.Array.Empty<int>(), 0, -1, 0, null, 0, 0, null, 0, false);
    }

    private static PostDto Rechirp(string id, string authorId, PostDto original)
    {
        return Post(id, authorId, string.Empty) with { RepostOfId = original.Id, ReferencedPost = original };
    }
}
