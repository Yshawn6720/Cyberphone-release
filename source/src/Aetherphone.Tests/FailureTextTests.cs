using System.Reflection;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FailureTextTests
{
    [Fact]
    public void EveryDeclaredCodeResolvesToRealText()
    {
        var codes = DeclaredCodes();

        Assert.True(codes.Count > 0, "No failure codes were discovered on FailureCodes.");

        for (var index = 0; index < codes.Count; index++)
        {
            var failure = new AepFailure(AepFailureKind.Server, 400, codes[index], null, "abc123", "4");
            var text = FailureText.Resolve(failure);

            Assert.False(string.IsNullOrWhiteSpace(text), $"Code '{codes[index]}' resolved to nothing.");
            Assert.DoesNotContain("{0}", text);
        }
    }

    [Fact]
    public void EveryTransportKindExceptCancelledResolvesToRealText()
    {
        var kinds = new[]
        {
            AepFailureKind.Offline, AepFailureKind.Timeout, AepFailureKind.RateLimitPaused,
            AepFailureKind.SignedOut, AepFailureKind.BadResponse,
        };

        for (var index = 0; index < kinds.Length; index++)
        {
            var text = FailureText.Resolve(AepFailure.Transport(kinds[index]));

            Assert.False(string.IsNullOrWhiteSpace(text), $"Kind '{kinds[index]}' resolved to nothing.");
            Assert.DoesNotContain("{0}", text);
        }
    }

    [Fact]
    public void SilentKindsResolveToNothingSoTheUserIsNotToldAboutTheirOwnCancellation()
    {
        Assert.Equal(string.Empty, FailureText.Resolve(AepFailure.None));
        Assert.Equal(string.Empty, FailureText.Resolve(AepFailure.Transport(AepFailureKind.Cancelled)));
    }

    [Fact]
    public void AnUnknownCodeFromANewerServerStillNamesTheReference()
    {
        var failure = new AepFailure(AepFailureKind.Server, 400, "a_code_this_client_has_never_heard_of", null,
            "ref9000", null);
        var text = FailureText.Resolve(failure);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("{0}", text);
        Assert.Contains("ref9000", text);
    }

    [Fact]
    public void AValuedCodeMissingItsValueDegradesInsteadOfPrintingThePlaceholder()
    {
        var failure = new AepFailure(AepFailureKind.Server, 400, FailureCodes.PostTooLong, null, "ref42", null);
        var text = FailureText.Resolve(failure);

        Assert.DoesNotContain("{0}", text);
        Assert.Contains("ref42", text);
    }

    [Fact]
    public void AValuedCodeCarryingItsValueInterpolatesIt()
    {
        var failure = new AepFailure(AepFailureKind.Server, 400, FailureCodes.PostTooLong, null, "ref42", "500");
        var text = FailureText.Resolve(failure);

        Assert.Contains("500", text);
        Assert.DoesNotContain("{0}", text);
    }

    [Fact]
    public void StatusOnlyFailuresStillSaySomethingSpecific()
    {
        Assert.Equal(Loc.T(L.Failure.Unauthorized), FailureText.Resolve(AepFailure.FromStatus(401, "r")));
        Assert.Equal(Loc.T(L.Failure.Forbidden), FailureText.Resolve(AepFailure.FromStatus(403, "r")));
        Assert.Equal(Loc.T(L.Failure.NotFound), FailureText.Resolve(AepFailure.FromStatus(404, "r")));
        Assert.Equal(Loc.T(L.Failure.RateLimited), FailureText.Resolve(AepFailure.FromStatus(429, "r")));
        Assert.Contains("r", FailureText.Resolve(AepFailure.FromStatus(500, "r")));
    }

    [Fact]
    public void ASlotHoldsItsTextAndClearsBackToNothing()
    {
        var slot = new FailureSlot();

        Assert.False(slot.Failed);
        Assert.Equal(string.Empty, slot.Text());

        slot.Set(AepFailure.Transport(AepFailureKind.Offline));

        Assert.True(slot.Failed);
        var first = slot.Text();
        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Same(first, slot.Text());

        slot.Clear();

        Assert.False(slot.Failed);
        Assert.Equal(string.Empty, slot.Text());
    }

    private static List<string> DeclaredCodes()
    {
        var fields = typeof(FailureCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
        var codes = new List<string>(fields.Length);
        for (var index = 0; index < fields.Length; index++)
        {
            if (fields[index].IsLiteral && fields[index].GetRawConstantValue() is string code)
            {
                codes.Add(code);
            }
        }

        return codes;
    }
}
