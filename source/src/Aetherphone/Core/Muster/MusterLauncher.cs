using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Muster;

internal sealed class MusterLauncher
{
    private readonly LaunchIntent detail = new();

    public void RequestDetail(string musterId) => detail.Request(musterId);

    public bool TryConsumeDetail(out string musterId) => detail.TryConsume(out musterId);
}
