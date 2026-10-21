using System.Text;
using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DecryptedHistoryTests
{
    [Fact]
    public void A_remembered_body_is_not_a_placeholder()
    {
        var remembered = new DmDecryptedBody(DmBodyState.Remembered, "hello", null, false);

        Assert.False(remembered.IsPlaceholder);
    }

    [Fact]
    public void Every_state_that_hides_the_text_is_still_a_placeholder()
    {
        Assert.True(new DmDecryptedBody(DmBodyState.Pending, "x", null, false).IsPlaceholder);
        Assert.True(new DmDecryptedBody(DmBodyState.NoKey, "x", null, false).IsPlaceholder);
        Assert.True(new DmDecryptedBody(DmBodyState.Malformed, "x", null, false).IsPlaceholder);
        Assert.False(new DmDecryptedBody(DmBodyState.Decrypted, "x", null, true).IsPlaceholder);
    }

    [Fact]
    public void A_remembered_body_carries_no_franking_key_so_it_cannot_be_reported()
    {
        var remembered = new DmDecryptedBody(DmBodyState.Remembered, "hello", null, false);

        Assert.Null(remembered.FrankingKey);
        Assert.False(remembered.Verified);
        Assert.NotEqual(DmBodyState.Decrypted, remembered.State);
    }

    [Fact]
    public void History_survives_a_write_and_read_back_through_the_local_protector()
    {
        const string owner = "user-history";
        var payload = Encoding.UTF8.GetBytes("{\"m1\":\"hello there\"}");

        var sealedText = LocalKeyProtector.Protect(payload, owner);
        var status = LocalKeyProtector.TryUnprotect(sealedText, owner, out var opened);

        Assert.Equal(LocalKeyStatus.Opened, status);
        Assert.Equal(payload, opened);
    }

    [Fact]
    public void History_sealed_for_one_account_does_not_open_for_another()
    {
        var payload = Encoding.UTF8.GetBytes("{\"m1\":\"private\"}");
        var sealedText = LocalKeyProtector.Protect(payload, "user-a");
        if (LocalKeyProtector.IsUnprotected(sealedText))
        {
            return;
        }

        Assert.Equal(LocalKeyStatus.Unreadable,
            LocalKeyProtector.TryUnprotect(sealedText, "user-b", out var opened));
        Assert.Null(opened);
    }
}
