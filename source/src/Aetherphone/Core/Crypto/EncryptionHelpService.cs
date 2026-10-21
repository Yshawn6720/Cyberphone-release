namespace Aetherphone.Core.Crypto;

internal sealed class EncryptionHelpService
{
    public bool Active { get; private set; }

    public void Open()
    {
        Active = true;
    }

    public void Dismiss()
    {
        Active = false;
    }
}
