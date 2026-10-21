namespace Aetherphone.Core.Apps;

internal sealed class VelvetLauncher
{
    private readonly LaunchIntent profile = new();

    public void Request(string userId) => profile.Request(userId);

    public bool TryConsume(out string userId) => profile.TryConsume(out userId);
}
