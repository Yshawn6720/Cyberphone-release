using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal static class BlockedContent
{
    public static PostDto[] Purge(PostDto[] posts, string userId)
    {
        var kept = CopyOnWrite.RemoveWhere(posts, post => Hides(post, userId));
        return CopyOnWrite.Map(kept, post => post.ReferencedPost?.AuthorId == userId,
            post => post with { ReferencedPost = null });
    }

    public static bool Hides(PostDto post, string userId)
    {
        return post.AuthorId == userId
            || (post.RepostOfId is not null && post.ReferencedPost?.AuthorId == userId);
    }
}
