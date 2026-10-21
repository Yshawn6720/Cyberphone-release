using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Crypto;

internal sealed class EncryptionSetupLauncher
{
    private readonly LaunchFlag pending = new();

    public void Request() => pending.Request();

    public bool TryConsume() => pending.TryConsume();
}
