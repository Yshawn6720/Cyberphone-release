namespace Aetherphone.Core.GameChat;

internal sealed class LinkpearlLauncher
{
    private string? pendingKey;
    private string? pendingLookupName;
    private string pendingLookupWorld = string.Empty;

    public void Request(string conversationKey) => pendingKey = conversationKey;

    public void RequestLookup(string name, string world)
    {
        pendingLookupName = name;
        pendingLookupWorld = world;
    }

    public bool TryConsume(out string conversationKey)
    {
        if (pendingKey is null)
        {
            conversationKey = string.Empty;
            return false;
        }

        conversationKey = pendingKey;
        pendingKey = null;
        return true;
    }

    public bool TryConsumeLookup(out string name, out string world)
    {
        if (pendingLookupName is null)
        {
            name = string.Empty;
            world = string.Empty;
            return false;
        }

        name = pendingLookupName;
        world = pendingLookupWorld;
        pendingLookupName = null;
        pendingLookupWorld = string.Empty;
        return true;
    }
}
