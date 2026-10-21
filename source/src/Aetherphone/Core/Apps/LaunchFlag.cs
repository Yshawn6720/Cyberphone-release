namespace Aetherphone.Core.Apps;

internal sealed class LaunchFlag
{
    private volatile bool pending;

    public void Request()
    {
        pending = true;
    }

    public bool TryConsume()
    {
        if (!pending)
        {
            return false;
        }

        pending = false;
        return true;
    }
}
