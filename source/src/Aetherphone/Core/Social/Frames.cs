namespace Aetherphone.Core.Social;

internal static class Frames
{
    private static FrameCatalogStore store = null!;

    public static void Use(FrameCatalogStore catalog)
    {
        store = catalog;
    }

    public static FrameStyle? Of(string? frameId)
    {
        return store.Find(frameId);
    }

    public static void EnsureFresh()
    {
        store.EnsureFresh();
    }
}
