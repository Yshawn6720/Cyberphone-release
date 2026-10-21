using System.Text;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RecoveryKeyTests
{
    [Fact]
    public void GeneratedCodeHasExpectedShape()
    {
        var code = RecoveryKey.GenerateCode();

        var groups = code.Split('-');
        Assert.Equal(5, groups.Length);
        for (var index = 0; index < groups.Length; index++)
        {
            Assert.Equal(4, groups[index].Length);
        }

        Assert.DoesNotContain('I', code);
        Assert.DoesNotContain('L', code);
        Assert.DoesNotContain('O', code);
        Assert.DoesNotContain('U', code);
    }

    [Fact]
    public void WrapUnwrapRoundTripsWithSameCode()
    {
        var identity = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(identity);
        var pkcs8 = CryptoBox.TryExportPrivateKey(identity);
        Assert.NotNull(pkcs8);
        var code = RecoveryKey.GenerateCode();

        var escrow = RecoveryKey.Wrap(pkcs8, code);
        Assert.NotNull(escrow);

        var recovered = RecoveryKey.Unwrap(escrow, code);
        Assert.NotNull(recovered);
        Assert.Equal(pkcs8, recovered);

        var imported = CryptoBox.ImportPrivateKey(recovered);
        Assert.NotNull(imported);
        Assert.Equal(CryptoBox.ExportPublicKey(identity), CryptoBox.ExportPublicKey(imported));
    }

    [Fact]
    public void WrongCodeFailsToUnwrap()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);

        var escrow = RecoveryKey.Wrap(pkcs8, RecoveryKey.GenerateCode());
        Assert.NotNull(escrow);

        Assert.Null(RecoveryKey.Unwrap(escrow, RecoveryKey.GenerateCode()));
    }

    [Fact]
    public void UnwrapToleratesFormattingAndAmbiguousCharacters()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);
        var escrow = RecoveryKey.Wrap(pkcs8, "ABCD-2345-6789-JKMN-PQRS");
        Assert.NotNull(escrow);

        var recovered = RecoveryKey.Unwrap(escrow, "abcd 2345 6789 jkmn pqrs");
        Assert.NotNull(recovered);
        Assert.Equal(pkcs8, recovered);
    }

    [Fact]
    public void CanonicalizeMapsAmbiguousCharacters()
    {
        Assert.Equal("0111VV", RecoveryKey.Canonicalize("oIlL-uV"));
    }

    [Theory]
    [InlineData("passwd", "salt", 1, "55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc")]
    [InlineData("Password", "NaCl", 80000, "4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56")]
    public void DeriveKeyMatchesRfc7914Pbkdf2HmacSha256Vector(string password, string salt, int iterations, string expectedHex)
    {
        var derived = RecoveryKey.DeriveKey(password, Encoding.UTF8.GetBytes(salt), iterations);

        Assert.Equal(Convert.FromHexString(expectedHex), derived);
    }

    [Fact]
    public void ArchivedEscrowChainUnwrapsEachKeyWithTheSameCode()
    {
        var code = RecoveryKey.GenerateCode();
        var anchors = new string[3];
        var escrows = new WrappedPrivateKeyDto[3];
        for (var index = 0; index < escrows.Length; index++)
        {
            var identity = CryptoBox.TryGenerateIdentity();
            Assert.NotNull(identity);
            anchors[index] = CryptoBox.ExportPublicKey(identity);
            var pkcs8 = CryptoBox.TryExportPrivateKey(identity);
            Assert.NotNull(pkcs8);
            var escrow = RecoveryKey.Wrap(pkcs8, code);
            Assert.NotNull(escrow);
            escrows[index] = escrow;
        }

        for (var index = 0; index < escrows.Length; index++)
        {
            var recovered = RecoveryKey.Unwrap(escrows[index], code);
            Assert.NotNull(recovered);
            var imported = CryptoBox.ImportPrivateKey(recovered);
            Assert.NotNull(imported);
            Assert.Equal(anchors[index], CryptoBox.ExportPublicKey(imported));
        }
    }

    [Fact]
    public void ArchivedEscrowRecoveryRejectsMismatchedPublicKeyAnchor()
    {
        var code = RecoveryKey.GenerateCode();
        var archivedIdentity = CryptoBox.TryGenerateIdentity();
        var otherIdentity = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(archivedIdentity);
        Assert.NotNull(otherIdentity);
        var pkcs8 = CryptoBox.TryExportPrivateKey(archivedIdentity);
        Assert.NotNull(pkcs8);
        var escrow = RecoveryKey.Wrap(pkcs8, code);
        Assert.NotNull(escrow);

        var recovered = RecoveryKey.Unwrap(escrow, code);
        Assert.NotNull(recovered);
        var imported = CryptoBox.ImportPrivateKey(recovered);
        Assert.NotNull(imported);

        var derivedPublicKey = CryptoBox.ExportPublicKey(imported);
        Assert.False(string.Equals(derivedPublicKey, CryptoBox.ExportPublicKey(otherIdentity), StringComparison.Ordinal));
        Assert.Equal(CryptoBox.ExportPublicKey(archivedIdentity), derivedPublicKey);
    }

    [Fact]
    public void WrongCodeFailsToUnwrapEveryEscrowInTheChain()
    {
        var code = RecoveryKey.GenerateCode();
        for (var index = 0; index < 2; index++)
        {
            var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
            Assert.NotNull(pkcs8);
            var escrow = RecoveryKey.Wrap(pkcs8, code);
            Assert.NotNull(escrow);
            Assert.Null(RecoveryKey.Unwrap(escrow, RecoveryKey.GenerateCode()));
            Assert.NotNull(RecoveryKey.Unwrap(escrow, code));
        }
    }

    [Fact]
    public void EscrowFieldsAreWellFormedForServerBounds()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);
        var escrow = RecoveryKey.Wrap(pkcs8, RecoveryKey.GenerateCode());
        Assert.NotNull(escrow);

        Assert.Equal(16, escrow.Nonce.Length);
        Assert.InRange(escrow.Salt.Length, 1, 88);
        Assert.InRange(escrow.Ciphertext.Length, 1, 4096);
        Assert.InRange(escrow.Iterations, 100_000, 2_000_000);
    }
}
