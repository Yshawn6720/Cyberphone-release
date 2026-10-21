namespace Aetherphone.Core.Social;

internal static class SensitiveReveals
{
    private static readonly HashSet<string> Revealed = new(StringComparer.Ordinal);

    public static bool ShouldVeil(bool sensitive, string postId, bool showAlways)
    {
        return sensitive && !showAlways && !Revealed.Contains(postId);
    }

    public static void Reveal(string postId)
    {
        Revealed.Add(postId);
    }

    public static void Clear()
    {
        Revealed.Clear();
    }
}
