using System.Collections.Concurrent;

namespace Aetherphone.Core.Crypto;

internal sealed class UnwrapFailureCache
{
    private readonly ConcurrentDictionary<(string ScopeId, int Generation), string> failedWrappedKeys = new();

    public bool ShouldSkip(string scopeId, int generation, string wrappedKey)
    {
        return failedWrappedKeys.TryGetValue((scopeId, generation), out var failedWrappedKey)
            && string.Equals(failedWrappedKey, wrappedKey, StringComparison.Ordinal);
    }

    public void RecordFailure(string scopeId, int generation, string wrappedKey, bool privateKeyWasLoaded)
    {
        if (!privateKeyWasLoaded)
        {
            return;
        }

        failedWrappedKeys[(scopeId, generation)] = wrappedKey;
    }

    public void RecordSuccess(string scopeId, int generation)
    {
        failedWrappedKeys.TryRemove((scopeId, generation), out _);
    }

    public void Clear()
    {
        failedWrappedKeys.Clear();
    }
}
