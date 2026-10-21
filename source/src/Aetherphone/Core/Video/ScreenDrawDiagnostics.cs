namespace Aetherphone.Core.Video;

internal enum ScreenDrawOutcome : byte
{
    Drawn,
    Hidden,
    NoSwapChain,
    NoBackBuffer,
    NoSceneDepth,
    ZeroSwapChainSize,
    ZeroRenderSize,
    NoD3DTexture,
    NoVideoTexture,
    NoCamera,
    ScaledDepthUnsampled,
    NoDepthStencilView,
    NoColorTargetView,
}

internal sealed class ScreenDrawDiagnostics
{
    internal const int MaxLoggedChanges = 24;

    private bool hasOutcome;
    private ScreenDrawOutcome lastOutcome;
    private int loggedChanges;

    internal bool ReachedLimit => loggedChanges >= MaxLoggedChanges;

    internal bool ShouldLog(ScreenDrawOutcome outcome)
    {
        if (hasOutcome && outcome == lastOutcome)
        {
            return false;
        }

        hasOutcome = true;
        lastOutcome = outcome;
        if (ReachedLimit)
        {
            return false;
        }

        loggedChanges++;
        return true;
    }
}
