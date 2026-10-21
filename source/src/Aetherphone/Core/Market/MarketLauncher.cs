namespace Aetherphone.Core.Market;

internal sealed class MarketLauncher
{
    private uint pendingItemId;
    private string? pendingSearch;
    private bool hasRequest;

    public void RequestItem(uint itemId)
    {
        pendingItemId = itemId;
        pendingSearch = null;
        hasRequest = true;
    }

    public void RequestSearch(string query)
    {
        pendingItemId = 0;
        pendingSearch = query;
        hasRequest = true;
    }

    public bool TryConsume(out uint itemId, out string? search)
    {
        if (!hasRequest)
        {
            itemId = 0;
            search = null;
            return false;
        }

        itemId = pendingItemId;
        search = pendingSearch;
        pendingItemId = 0;
        pendingSearch = null;
        hasRequest = false;
        return true;
    }
}
