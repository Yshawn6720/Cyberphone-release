using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class EncryptionPage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Encryption.Title);

    public string Summary => vault.State switch
    {
        KeyVaultState.Unlocked when vault.UnsavedRecoveryCode is not null =>
            Loc.T(L.Encryption.SummaryUnsavedCode),
        KeyVaultState.Unlocked when !vault.RecoveryConfigured => Loc.T(L.Encryption.SummaryNoRecovery),
        KeyVaultState.Unlocked => Loc.T(L.Encryption.StateActive),
        KeyVaultState.Provisioning => Loc.T(L.Encryption.StateSettingUp),
        KeyVaultState.Locked => Loc.T(L.Encryption.StateLocked),
        KeyVaultState.Unsupported => Loc.T(L.Encryption.StateUnsupported),
        _ => Loc.T(L.Encryption.StateUnavailable),
    };

    public FontAwesomeIcon Icon => FontAwesomeIcon.Lock;
    public Vector4 Tint => new(0.38f, 0.66f, 0.42f, 1f);

    private const int RecoveryCodeMaxLength = 64;
    private readonly AethernetSession session;
    private readonly KeyVault vault;
    private readonly EncryptionVaultActions actions;
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool refreshRequested;
    private int lastDrawnFrame;
    private bool restoreEntryOpen;

    public EncryptionPage(AethernetSession session, KeyVault vault, ConfirmService confirm)
    {
        this.session = session;
        this.vault = vault;
        actions = new EncryptionVaultActions(vault, confirm);
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var frame = ImGui.GetFrameCount();
        if (frame - lastDrawnFrame > 1)
        {
            restoreEntryOpen = false;
            actions.RefreshArchivedEscrows();
        }

        lastDrawnFrame = frame;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            EnsureRefreshed();
            if (actions.GeneratedCode.Length > 0)
            {
                DrawGeneratedCode(theme);
            }
            else
            {
                switch (vault.State)
                {
                    case KeyVaultState.Unavailable:
                        DrawUnavailable(theme);
                        break;
                    case KeyVaultState.Provisioning:
                        DrawProvisioning(theme);
                        break;
                    case KeyVaultState.Unsupported:
                        DrawUnsupported(theme);
                        break;
                    case KeyVaultState.Locked:
                        DrawLocked(theme);
                        break;
                    default:
                        DrawActive(theme);
                        break;
                }
            }

            DrawStatus(theme);
        }
    }

    private void EnsureRefreshed()
    {
        if (refreshRequested || !session.IsSignedIn || session.CurrentUser is null || vault.IsRefreshing)
        {
            if (!session.IsSignedIn)
            {
                refreshRequested = false;
            }

            return;
        }

        refreshRequested = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await vault.RefreshAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Encryption key refresh failed");
            }
        });
    }

    private static void DrawUnavailable(PhoneTheme theme)
    {
        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsForm.Body(Loc.T(L.Encryption.NotSignedIn), theme);
    }

    private static void DrawUnsupported(PhoneTheme theme)
    {
        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsForm.Body(Loc.T(L.Encryption.UnsupportedBody), theme);
    }

    private static void DrawProvisioning(PhoneTheme theme)
    {
        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsForm.Body(Loc.T(L.Encryption.Intro), theme);
        SettingsForm.Gap(Metrics.Space.Xs);
        SettingsForm.Body(Loc.T(L.Encryption.SettingUp), theme);
    }

    private void DrawLocked(PhoneTheme theme)
    {
        if (vault.RecoveryConfigured)
        {
            DrawLockedRecover(theme);
            return;
        }

        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsForm.Body(vault.LocalKeyUnreadable
            ? Loc.T(L.Encryption.UnreadableKeyBody)
            : Loc.T(L.Encryption.LockedNoRecoveryBody), theme);
        SettingsForm.Gap(Metrics.Space.Xl);
        if (SettingsForm.Button(Loc.T(L.Encryption.NewKeyButton), theme, ButtonStyle.Gray, ButtonRole.Destructive)
            && !actions.Busy)
        {
            actions.AskResetWithoutRecovery();
        }
    }

    private void DrawLockedRecover(PhoneTheme theme)
    {
        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsForm.Body(Loc.T(L.Encryption.LockedRecoverBody), theme);
        SettingsForm.Gap(Metrics.Space.Md);
        DrawCodeInput(theme);
        SettingsForm.Gap(Metrics.Space.Xl);
        if (SettingsForm.Button(Loc.T(L.Encryption.RecoveryUnlockButton), theme, ButtonStyle.Prominent,
                enabled: RecoveryKey.Canonicalize(actions.CodeEntry).Length > 0) && !actions.Busy)
        {
            actions.BeginRecover();
        }

        SettingsForm.Gap(Metrics.Space.Sm);
        if (SettingsForm.Button(Loc.T(L.Encryption.NewKeyButton), theme, ButtonStyle.Gray, ButtonRole.Destructive)
            && !actions.Busy)
        {
            actions.AskReset();
        }
    }

    private void DrawCodeInput(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Encryption.RecoveryCodeLabel), theme);
        SettingsForm.TextField("##recoveryCode", string.Empty, ref actions.CodeEntry, theme, RecoveryCodeMaxLength);
    }

    private void DrawGeneratedCode(PhoneTheme theme)
    {
        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsForm.Title(Loc.T(L.Encryption.RecoverySaveTitle), theme);
        SettingsForm.Gap(Metrics.Space.Lg);
        DrawCopyableCode(actions.GeneratedCode, theme);
        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsSection.Hint(Loc.T(L.Encryption.RecoverySaveBody), theme);
        SettingsForm.Gap(Metrics.Space.Xl);
        if (SettingsForm.Button(Loc.T(L.Encryption.RecoveryCopy), theme))
        {
            CopyCode(actions.GeneratedCode);
        }

        SettingsForm.Gap(Metrics.Space.Sm);
        if (SettingsForm.Button(Loc.T(L.Encryption.RecoverySavedButton), theme, ButtonStyle.Prominent))
        {
            actions.AcknowledgeGeneratedCode();
        }
    }

    private static void DrawCopyableCode(string code, PhoneTheme theme)
    {
        if (SettingsForm.CodeCard(code, theme))
        {
            CopyCode(code);
        }
    }

    private static void CopyCode(string code)
    {
        ImGui.SetClipboardText(code);
        ShellToast.Show();
    }

    private void DrawRecoverySection(PhoneTheme theme)
    {
        var unsaved = vault.UnsavedRecoveryCode;
        if (unsaved is not null)
        {
            DrawUnsavedCodeSection(theme, unsaved);
            return;
        }

        SettingsForm.Gap(Metrics.Space.Lg);
        SettingsSection.Header(Loc.T(L.Encryption.RecoverySectionTitle), theme);
        var label = vault.RecoveryConfigured
            ? Loc.T(L.Encryption.RecoveryRegenerateButton)
            : Loc.T(L.Encryption.RecoverySetupButton);
        if (SettingsForm.ActionCard(label, theme.Accent, theme, !actions.Busy))
        {
            actions.BeginCreateRecoveryCode();
        }

        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsSection.Hint(vault.RecoveryConfigured
            ? Loc.T(L.Encryption.RecoveryConfiguredBody)
            : Loc.T(L.Encryption.RecoveryNotSetBody), theme);
    }

    private void DrawActive(PhoneTheme theme)
    {
        SettingsForm.Gap(Metrics.Space.Xs);
        SettingsForm.Body(Loc.T(L.Encryption.Intro), theme);
        SettingsForm.Gap(Metrics.Space.Xs);
        SettingsForm.Body(Loc.T(L.Encryption.ActiveHint), theme);
        SettingsForm.Gap(Metrics.Space.Xs);
        SettingsForm.Body(Loc.T(L.Encryption.NewDeviceHint), theme);
        SettingsForm.Gap(Metrics.Space.Xs);
        SettingsForm.Note(Loc.T(L.Encryption.KeyVersion, vault.KeyVersion), theme);
        if (vault.LocalCacheUnavailable)
        {
            SettingsForm.Gap(Metrics.Space.Sm);
            SettingsForm.Note(Loc.T(L.Encryption.LocalStoreUnavailable), theme);
        }

        DrawRecoverySection(theme);
        DrawRestoreOlderSection(theme);
        SettingsForm.Gap(Metrics.Space.Xl);
        if (SettingsForm.ActionCard(Loc.T(L.Encryption.ResetButton), theme.Danger, theme, !actions.Busy))
        {
            actions.AskReset();
        }
    }

    private void DrawUnsavedCodeSection(PhoneTheme theme, string code)
    {
        SettingsForm.Gap(Metrics.Space.Xl);
        if (actions.VerifyingSavedCode)
        {
            SettingsForm.Heading(Loc.T(L.Encryption.GuideVerifyTitle), theme);
            SettingsForm.Gap(Metrics.Space.Xs);
            SettingsForm.Body(Loc.T(L.Encryption.GuideVerifyBody, actions.ExpectedVerifyGroup ?? string.Empty), theme);
            SettingsForm.Gap(Metrics.Space.Md);
            SettingsForm.TextField("##encryptionVerifySettings", string.Empty, ref actions.VerifyEntry, theme,
                RecoveryCodeMaxLength);
            SettingsForm.Gap(Metrics.Space.Lg);
            if (SettingsForm.Button(Loc.T(L.Encryption.GuideVerifyConfirm), theme, ButtonStyle.Prominent))
            {
                actions.TryConfirmSavedCode();
            }

            SettingsForm.Gap(Metrics.Space.Sm);
            if (SettingsForm.Button(Loc.T(L.Encryption.GuideShowAgain), theme))
            {
                actions.VerifyingSavedCode = false;
                actions.Status = string.Empty;
            }

            return;
        }

        SettingsForm.Heading(Loc.T(L.Encryption.RecoverySaveTitle), theme);
        SettingsForm.Gap(Metrics.Space.Xs);
        SettingsForm.Body(Loc.T(L.Encryption.SaveCodeIntro), theme);
        SettingsForm.Gap(Metrics.Space.Md);
        DrawCopyableCode(code, theme);
        SettingsForm.Gap(Metrics.Space.Lg);
        if (SettingsForm.Button(Loc.T(L.Encryption.RecoveryCopy), theme))
        {
            CopyCode(code);
        }

        SettingsForm.Gap(Metrics.Space.Sm);
        if (SettingsForm.Button(Loc.T(L.Encryption.GuideWroteItDown), theme, ButtonStyle.Prominent))
        {
            actions.VerifyingSavedCode = true;
            actions.Status = string.Empty;
        }
    }

    private void DrawRestoreOlderSection(PhoneTheme theme)
    {
        var olderKeysHeldHere = actions.Vault.OlderKeysHeldHere;
        if (!actions.HasArchivedEscrows && olderKeysHeldHere == 0)
        {
            return;
        }

        SettingsForm.Gap(Metrics.Space.Lg);
        SettingsSection.Header(Loc.T(L.Encryption.RestoreOlderTitle), theme);
        if (actions.HasArchivedEscrows && !restoreEntryOpen)
        {
            if (SettingsForm.ActionCard(Loc.T(L.Encryption.RestoreOlderButton), theme.Accent, theme, !actions.Busy))
            {
                actions.CodeEntry = string.Empty;
                restoreEntryOpen = true;
            }

            SettingsForm.Gap(Metrics.Space.Sm);
        }

        SettingsSection.Hint(Loc.T(L.Encryption.RestoreOlderBody), theme);
        if (olderKeysHeldHere > 0)
        {
            SettingsSection.Hint(Loc.T(L.Encryption.OlderKeysHeldHere, olderKeysHeldHere), theme);
        }

        if (!actions.HasArchivedEscrows || !restoreEntryOpen)
        {
            return;
        }

        SettingsForm.Gap(Metrics.Space.Md);
        DrawCodeInput(theme);
        SettingsForm.Gap(Metrics.Space.Lg);
        if (SettingsForm.Button(Loc.T(L.Encryption.RestoreOlderConfirm), theme, ButtonStyle.Prominent,
                enabled: RecoveryKey.Canonicalize(actions.CodeEntry).Length > 0) && !actions.Busy)
        {
            actions.BeginRestorePreviousKeys();
        }
    }

    private void DrawStatus(PhoneTheme theme)
    {
        var message = actions.Status;
        if (message.Length == 0)
        {
            return;
        }

        SettingsForm.Gap(Metrics.Space.Sm);
        SettingsForm.Body(message, theme);
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
        actions.Dispose();
    }
}
