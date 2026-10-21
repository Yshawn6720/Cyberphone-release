using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;

namespace Aetherphone.Windows.Components;

internal sealed class EncryptionVaultActions : IDisposable
{
    private readonly KeyVault vault;
    private readonly ConfirmService confirm;
    private readonly CancellationTokenSource cancellation = new();
    private volatile string status = string.Empty;
    private volatile bool busy;
    private volatile string generatedCode = string.Empty;
    private volatile bool hasArchivedEscrows;
    private volatile string linkRequestId = string.Empty;
    private volatile string linkCode = string.Empty;
    private volatile bool linkPolling;
    private float sinceLinkPoll;

    private const float LinkPollSeconds = 2f;

    public string CodeEntry = string.Empty;

    public EncryptionVaultActions(KeyVault vault, ConfirmService confirm)
    {
        this.vault = vault;
        this.confirm = confirm;
    }

    public KeyVault Vault => vault;

    public bool Busy => busy;

    public string GeneratedCode => generatedCode;

    public bool HasArchivedEscrows => hasArchivedEscrows;

    public string Status
    {
        get => status;
        set => status = value;
    }

    public void AcknowledgeGeneratedCode()
    {
        generatedCode = string.Empty;
        status = string.Empty;
    }

    public string VerifyEntry = string.Empty;

    public bool VerifyingSavedCode;

    public string? ExpectedVerifyGroup
    {
        get
        {
            var code = vault.UnsavedRecoveryCode;
            if (code is null)
            {
                return null;
            }

            var groups = code.Split('-');
            return groups.Length == 0 ? null : groups[^1];
        }
    }

    public bool TryConfirmSavedCode()
    {
        var code = vault.UnsavedRecoveryCode;
        var expected = ExpectedVerifyGroup;
        if (code is null || expected is null)
        {
            return false;
        }

        var typed = RecoveryKey.Canonicalize(VerifyEntry);
        if (!string.Equals(typed, RecoveryKey.Canonicalize(expected), StringComparison.Ordinal)
            && !string.Equals(typed, RecoveryKey.Canonicalize(code), StringComparison.Ordinal))
        {
            status = Loc.T(L.Encryption.GuideVerifyWrong);
            return false;
        }

        VerifyEntry = string.Empty;
        VerifyingSavedCode = false;
        status = string.Empty;
        vault.AcknowledgeRecoveryCode();
        return true;
    }

    public string LinkCode => linkCode;

    public bool LinkWaiting => linkRequestId.Length > 0;

    public void BeginDeviceLink()
    {
        if (busy || linkRequestId.Length > 0)
        {
            return;
        }

        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var ticket = await vault.StartDeviceLinkAsync(cancellation.Token).ConfigureAwait(false);
                if (ticket is null)
                {
                    status = Loc.T(L.Encryption.Failed);
                    return;
                }

                linkRequestId = ticket.Id;
                linkCode = ticket.VerificationCode;
                status = string.Empty;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Starting a device link failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void CancelDeviceLink()
    {
        var requestId = linkRequestId;
        linkRequestId = string.Empty;
        linkCode = string.Empty;
        if (requestId.Length > 0)
        {
            vault.CancelDeviceLink(requestId);
        }
    }

    public void TickDeviceLink(float deltaSeconds)
    {
        var requestId = linkRequestId;
        if (requestId.Length == 0 || linkPolling)
        {
            return;
        }

        sinceLinkPoll += deltaSeconds;
        if (sinceLinkPoll < LinkPollSeconds)
        {
            return;
        }

        sinceLinkPoll = 0f;
        linkPolling = true;
        _ = Task.Run(async () =>
        {
            try
            {
                if (await vault.TryCompleteDeviceLinkAsync(requestId, cancellation.Token).ConfigureAwait(false))
                {
                    linkRequestId = string.Empty;
                    linkCode = string.Empty;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Completing a device link failed");
            }
            finally
            {
                linkPolling = false;
            }
        });
    }

    public void AskReset()
    {
        AskReset(vault.RecoveryConfigured
            ? Loc.T(L.Encryption.ForgotBody)
            : Loc.T(L.Encryption.ForgotNoRecoveryBody));
    }

    public void AskResetWithoutRecovery()
    {
        AskReset(Loc.T(L.Encryption.ForgotNoRecoveryBody));
    }

    private void AskReset(string message)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = message,
            ConfirmLabel = Loc.T(L.Encryption.ForgotConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done =>
            {
                Reset();
                done(true);
            },
        });
    }

    public void BeginCreateRecoveryCode()
    {
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var creation = await vault.CreateRecoveryCodeAsync(cancellation.Token).ConfigureAwait(false);
                if (creation.Status == RecoveryCodeCreationStatus.Created && creation.Code is not null)
                {
                    generatedCode = creation.Code;
                    status = string.Empty;
                }
                else if (creation.Status == RecoveryCodeCreationStatus.KeyChangedElsewhere)
                {
                    status = Loc.T(L.Encryption.RecoveryKeyChanged);
                }
                else
                {
                    status = Loc.T(L.Encryption.Failed);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Recovery code setup failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void BeginRecover()
    {
        var code = CodeEntry;
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var outcome = await vault.RecoverWithCodeAsync(code, cancellation.Token).ConfigureAwait(false);
                if (outcome == RecoveryAttemptOutcome.Recovered)
                {
                    CodeEntry = string.Empty;
                    status = string.Empty;
                    await RestorePreviousKeysSilentlyAsync(code).ConfigureAwait(false);
                }
                else if (outcome == RecoveryAttemptOutcome.OlderCode)
                {
                    CodeEntry = string.Empty;
                    status = Loc.T(L.Encryption.RecoveryOlderCode);
                }
                else if (outcome == RecoveryAttemptOutcome.WrongCode)
                {
                    status = Loc.T(L.Encryption.RecoveryWrongCode);
                }
                else
                {
                    status = Loc.T(L.Encryption.Failed);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Recovery failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    private async Task RestorePreviousKeysSilentlyAsync(string code)
    {
        try
        {
            var restored = await vault.RestorePreviousKeysAsync(code, cancellation.Token).ConfigureAwait(false);
            if (restored > 0)
            {
                status = Loc.T(L.Encryption.RestoreOlderDone, restored);
            }
            else if (restored < 0)
            {
                status = Loc.T(L.Encryption.RestoreOlderRetry);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Previous key restore failed");
            status = Loc.T(L.Encryption.RestoreOlderRetry);
        }
    }

    public void RefreshArchivedEscrows()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                hasArchivedEscrows = await vault.HasArchivedEscrowsAsync(cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Archived escrow lookup failed");
            }
        });
    }

    public void BeginRestorePreviousKeys()
    {
        var code = CodeEntry;
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var restored = await vault.RestorePreviousKeysAsync(code, cancellation.Token).ConfigureAwait(false);
                if (restored > 0)
                {
                    CodeEntry = string.Empty;
                    status = Loc.T(L.Encryption.RestoreOlderDone, restored);
                }
                else if (restored < 0)
                {
                    status = Loc.T(L.Encryption.Failed);
                }
                else
                {
                    status = Loc.T(L.Encryption.RestoreOlderNoMatch);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Previous key restore failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    private void Reset()
    {
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var succeeded = await vault.ResetAsync(cancellation.Token).ConfigureAwait(false);
                status = succeeded ? string.Empty : Loc.T(L.Encryption.Failed);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Encryption reset failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
