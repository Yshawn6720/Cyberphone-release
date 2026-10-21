using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Crypto;

internal enum EncryptionGuideStep : byte
{
    None = 0,
    SaveCode = 1,
    Locked = 2,
}

internal sealed class EncryptionGuide
{
    private static readonly Vector4 Accent = new(0.42f, 0.36f, 0.86f, 1f);

    private readonly KeyVault vault;
    private readonly AethernetSession session;
    private readonly NotificationService notifications;
    private EncryptionGuideStep announced;
    private float sinceCheck;

    private const float CheckSeconds = 5f;

    public EncryptionGuide(KeyVault vault, AethernetSession session, NotificationService notifications)
    {
        this.vault = vault;
        this.session = session;
        this.notifications = notifications;
    }

    public EncryptionGuideStep Step
    {
        get
        {
            if (!session.IsSignedIn)
            {
                return EncryptionGuideStep.None;
            }

            if (vault.State == KeyVaultState.Locked)
            {
                return EncryptionGuideStep.Locked;
            }

            if (vault.State == KeyVaultState.Unlocked && vault.UnsavedRecoveryCode is not null)
            {
                return EncryptionGuideStep.SaveCode;
            }

            return EncryptionGuideStep.None;
        }
    }

    public void Tick(float deltaSeconds)
    {
        sinceCheck += deltaSeconds;
        if (sinceCheck < CheckSeconds)
        {
            return;
        }

        sinceCheck = 0f;
        var step = Step;
        if (step == announced)
        {
            return;
        }

        announced = step;
        if (step == EncryptionGuideStep.None)
        {
            notifications.RemoveApp(AppId);
            return;
        }

        var title = step == EncryptionGuideStep.SaveCode
            ? Loc.T(L.Encryption.GuideSaveTitle)
            : Loc.T(L.Encryption.GuideLockedTitle);
        var body = step == EncryptionGuideStep.SaveCode
            ? Loc.T(L.Encryption.GuideSaveBody)
            : Loc.T(L.Encryption.GuideLockedBody);
        notifications.Notify(new PhoneNotification(AppId, title, body, DateTime.Now, Accent, GroupKey));
    }

    public const string AppId = "settings";

    public const string GroupKey = "encryption.guide";
}
