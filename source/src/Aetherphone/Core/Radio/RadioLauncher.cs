using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Radio;

internal sealed class RadioLauncher
{
    private readonly LaunchIntent station = new();

    public void RequestStation(string stationId) => station.Request(stationId);

    public bool TryConsumeStation(out string stationId) => station.TryConsume(out stationId);
}
