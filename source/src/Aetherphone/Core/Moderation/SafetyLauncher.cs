using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Moderation;

internal sealed class SafetyLauncher
{
    private readonly LaunchFlag pending = new();

    public void Request() => pending.Request();

    public bool TryConsume() => pending.TryConsume();
}
