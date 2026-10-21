namespace Aetherphone.Core.Apps;

internal sealed class LaunchIntent
{
    private string? pending;

    public void Request(string value)
    {
        pending = value;
    }

    public bool TryConsume(out string value)
    {
        if (pending is null)
        {
            value = string.Empty;
            return false;
        }

        value = pending;
        pending = null;
        return true;
    }
}
