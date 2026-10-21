using System.Text;
using Aetherphone;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class EncryptionKeyStoreTests
{
    [Fact]
    public void Migration_adopts_the_flat_key()
    {
        var configuration = new Configuration
        {
            EncryptionKeyCache = "blob-a",
            EncryptionKeyCacheUserId = "user-a",
        };

        Assert.True(configuration.SeedEncryptionKeyStore());
        Assert.Equal("blob-a", configuration.EncryptionKeysByUserId["user-a"]);
    }

    [Fact]
    public void Migration_adopts_keys_parked_by_the_character_session_migration()
    {
        var configuration = new Configuration
        {
            LegacyUnclaimedEncryptionKey = "blob-legacy",
            LegacyUnclaimedEncryptionUserId = "user-legacy",
        };

        configuration.SeedEncryptionKeyStore();

        Assert.Equal("blob-legacy", configuration.EncryptionKeysByUserId["user-legacy"]);
    }

    [Fact]
    public void Migration_adopts_every_character_snapshot_key()
    {
        var configuration = new Configuration();
        configuration.CharacterSessions[1] = new CharacterSession
        {
            EncryptionKeyCache = "blob-one",
            EncryptionKeyCacheUserId = "user-one",
        };
        configuration.CharacterSessions[2] = new CharacterSession
        {
            EncryptionKeyCache = "blob-two",
            EncryptionKeyCacheUserId = "user-two",
        };

        configuration.SeedEncryptionKeyStore();

        Assert.Equal("blob-one", configuration.EncryptionKeysByUserId["user-one"]);
        Assert.Equal("blob-two", configuration.EncryptionKeysByUserId["user-two"]);
    }

    [Fact]
    public void Migration_keeps_both_accounts_that_shared_one_character_slot()
    {
        var configuration = new Configuration
        {
            EncryptionKeyCache = "blob-active",
            EncryptionKeyCacheUserId = "user-active",
        };
        configuration.CharacterSessions[1] = new CharacterSession
        {
            EncryptionKeyCache = "blob-displaced",
            EncryptionKeyCacheUserId = "user-displaced",
        };

        configuration.SeedEncryptionKeyStore();

        Assert.Equal(2, configuration.EncryptionKeysByUserId.Count);
        Assert.Equal("blob-active", configuration.EncryptionKeysByUserId["user-active"]);
        Assert.Equal("blob-displaced", configuration.EncryptionKeysByUserId["user-displaced"]);
    }

    [Fact]
    public void Migration_never_overwrites_an_adopted_key_and_runs_once()
    {
        var configuration = new Configuration
        {
            EncryptionKeyCache = "blob-flat",
            EncryptionKeyCacheUserId = "user-a",
        };
        configuration.CharacterSessions[1] = new CharacterSession
        {
            EncryptionKeyCache = "blob-stale",
            EncryptionKeyCacheUserId = "user-a",
        };

        configuration.SeedEncryptionKeyStore();
        Assert.Equal("blob-flat", configuration.EncryptionKeysByUserId["user-a"]);

        configuration.EncryptionKeysByUserId["user-a"] = "blob-current";
        Assert.False(configuration.SeedEncryptionKeyStore());
        Assert.Equal("blob-current", configuration.EncryptionKeysByUserId["user-a"]);
    }

    [Fact]
    public void Migration_ignores_empty_slots()
    {
        var configuration = new Configuration
        {
            EncryptionKeyCache = string.Empty,
            EncryptionKeyCacheUserId = "user-a",
        };
        configuration.CharacterSessions[1] = new CharacterSession
        {
            EncryptionKeyCache = "blob-orphan",
            EncryptionKeyCacheUserId = string.Empty,
        };

        configuration.SeedEncryptionKeyStore();

        Assert.Empty(configuration.EncryptionKeysByUserId);
    }

    [Fact]
    public void Unprotect_separates_a_missing_key_from_an_unreadable_one()
    {
        Assert.Equal(LocalKeyStatus.Missing, LocalKeyProtector.TryUnprotect(string.Empty, "user-a", out var missing));
        Assert.Null(missing);

        Assert.Equal(LocalKeyStatus.Unreadable, LocalKeyProtector.TryUnprotect("not-base64!!", "user-a", out var broken));
        Assert.Null(broken);
    }

    [Fact]
    public void Unprotect_opens_a_key_that_was_stored_without_platform_protection()
    {
        var secret = Encoding.UTF8.GetBytes("private-key-bytes");
        var stored = "raw." + Convert.ToBase64String(secret);

        Assert.Equal(LocalKeyStatus.Opened, LocalKeyProtector.TryUnprotect(stored, "user-a", out var opened));
        Assert.Equal(secret, opened);
    }

    [Fact]
    public void A_linked_device_recovers_the_exact_identity_key()
    {
        var identity = CryptoBox.TryGenerateIdentity();
        var ephemeral = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(identity);
        Assert.NotNull(ephemeral);

        var pkcs8 = CryptoBox.TryExportPrivateKey(identity!);
        Assert.NotNull(pkcs8);

        var wrapped = CryptoBox.WrapSecret(pkcs8!, CryptoBox.ExportPublicKey(ephemeral!));
        Assert.NotNull(wrapped);

        var opened = CryptoBox.UnwrapSecret(wrapped!, ephemeral!);
        Assert.Equal(pkcs8, opened);

        var reimported = CryptoBox.ImportPrivateKey(opened!);
        Assert.NotNull(reimported);
        Assert.Equal(CryptoBox.ExportPublicKey(identity!), CryptoBox.ExportPublicKey(reimported!));
    }

    [Fact]
    public void A_link_wrap_does_not_open_for_a_different_device()
    {
        var secret = Encoding.UTF8.GetBytes("an identity key");
        var intended = CryptoBox.TryGenerateIdentity();
        var attacker = CryptoBox.TryGenerateIdentity();
        var wrapped = CryptoBox.WrapSecret(secret, CryptoBox.ExportPublicKey(intended!));

        Assert.NotNull(wrapped);
        Assert.Null(CryptoBox.UnwrapSecret(wrapped!, attacker!));
    }

    [Fact]
    public void Link_wraps_and_conversation_key_wraps_cannot_be_confused()
    {
        var recipient = CryptoBox.TryGenerateIdentity();
        var publicKey = CryptoBox.ExportPublicKey(recipient!);
        var cek = CryptoBox.GenerateCek();

        var cekWrap = CryptoBox.WrapCek(cek, publicKey);
        var linkWrap = CryptoBox.WrapSecret(Encoding.UTF8.GetBytes("identity key bytes"), publicKey);
        Assert.NotNull(cekWrap);
        Assert.NotNull(linkWrap);

        Assert.StartsWith("EC1.", cekWrap!, StringComparison.Ordinal);
        Assert.StartsWith("EL1.", linkWrap!, StringComparison.Ordinal);
        Assert.Null(CryptoBox.UnwrapSecret(cekWrap!, recipient!));
        Assert.Null(CryptoBox.UnwrapCek(linkWrap!, recipient!));
    }

    [Fact]
    public void A_key_archived_on_this_device_still_opens_wraps_the_current_key_cannot()
    {
        var retired = CryptoBox.TryGenerateIdentity();
        var current = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(retired);
        Assert.NotNull(current);

        var cek = CryptoBox.GenerateCek();
        var wrap = CryptoBox.WrapCek(cek, CryptoBox.ExportPublicKey(retired!));
        Assert.NotNull(wrap);
        Assert.Null(CryptoBox.UnwrapCek(wrap!, current!));

        var pkcs8 = CryptoBox.TryExportPrivateKey(retired!);
        Assert.NotNull(pkcs8);

        var archived = LocalKeyProtector.Protect(pkcs8!, "user-a");
        Assert.Equal(LocalKeyStatus.Opened, LocalKeyProtector.TryUnprotect(archived, "user-a", out var reopened));

        var reimported = CryptoBox.ImportPrivateKey(reopened!);
        Assert.NotNull(reimported);
        Assert.Equal(cek, CryptoBox.UnwrapCek(wrap!, reimported!));
    }

    [Fact]
    public void A_generated_recovery_code_ends_in_a_group_that_can_be_typed_back()
    {
        var code = RecoveryKey.GenerateCode();
        var groups = code.Split('-');

        Assert.Equal(5, groups.Length);
        Assert.All(groups, group => Assert.Equal(4, group.Length));
        Assert.Equal(RecoveryKey.Canonicalize(groups[^1]), RecoveryKey.Canonicalize(groups[^1].ToLowerInvariant()));
    }

    [Fact]
    public void Recovery_nudge_returns_after_the_snooze_expires()
    {
        var configuration = new Configuration();
        Assert.True(configuration.RecoveryNudgeDue());

        configuration.MarkRecoveryNudgeSnoozed();
        Assert.False(configuration.RecoveryNudgeDue());

        configuration.EncryptionRecoveryNudgeSnoozedUntilUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
        Assert.True(configuration.RecoveryNudgeDue());
    }

    [Fact]
    public void A_snooze_supersedes_the_old_permanent_dismissal()
    {
        var configuration = new Configuration { EncryptionRecoveryNudgeDismissed = true };
        Assert.False(configuration.RecoveryNudgeDue());

        configuration.EncryptionRecoveryNudgeSnoozedUntilUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
        Assert.True(configuration.RecoveryNudgeDue());
    }
}
