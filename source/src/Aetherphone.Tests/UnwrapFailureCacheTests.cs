using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UnwrapFailureCacheTests
{
    private const string Scope = "velvet:alice:bob";
    private const int Generation = 3;
    private const string FailedWrap = "EC1.original-wrap";
    private const string HealedWrap = "EC1.healed-wrap";

    [Fact]
    public void RecordedFailureSkipsTheSameWrappedKey()
    {
        var cache = new UnwrapFailureCache();
        cache.RecordFailure(Scope, Generation, FailedWrap, true);

        Assert.True(cache.ShouldSkip(Scope, Generation, FailedWrap));
    }

    [Fact]
    public void DifferentWrappedKeyForTheSameScopeAndGenerationIsRetried()
    {
        var cache = new UnwrapFailureCache();
        cache.RecordFailure(Scope, Generation, FailedWrap, true);

        Assert.False(cache.ShouldSkip(Scope, Generation, HealedWrap));
    }

    [Fact]
    public void FailureWithoutALoadedPrivateKeyIsNotMemoized()
    {
        var cache = new UnwrapFailureCache();
        cache.RecordFailure(Scope, Generation, FailedWrap, false);

        Assert.False(cache.ShouldSkip(Scope, Generation, FailedWrap));
    }

    [Fact]
    public void SuccessClearsARecordedFailure()
    {
        var cache = new UnwrapFailureCache();
        cache.RecordFailure(Scope, Generation, FailedWrap, true);
        cache.RecordSuccess(Scope, Generation);

        Assert.False(cache.ShouldSkip(Scope, Generation, FailedWrap));
    }

    [Fact]
    public void FailuresAreScopedPerGeneration()
    {
        var cache = new UnwrapFailureCache();
        cache.RecordFailure(Scope, Generation, FailedWrap, true);

        Assert.False(cache.ShouldSkip(Scope, Generation + 1, FailedWrap));
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var cache = new UnwrapFailureCache();
        cache.RecordFailure(Scope, Generation, FailedWrap, true);
        cache.Clear();

        Assert.False(cache.ShouldSkip(Scope, Generation, FailedWrap));
    }
}
