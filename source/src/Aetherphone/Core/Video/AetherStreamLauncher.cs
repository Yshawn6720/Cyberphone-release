using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Video;

internal sealed class AetherStreamLauncher
{
    private readonly LaunchFlag upNext = new();

    public void RequestUpNext() => upNext.Request();

    public bool TryConsumeUpNext() => upNext.TryConsume();
}
