using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class RadioClient
{
    private readonly AethernetTransport net;

    public RadioClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CommunityStationPage?> StationsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/radio/stations", AethernetJsonContext.Default.CommunityStationPage, token, null,
            onFailure);
    }

    public Task<CommunityStationDto?> StationAsync(string stationId, CancellationToken token,
        Action<int>? statusSink = null, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/radio/stations/{Uri.EscapeDataString(stationId)}",
            AethernetJsonContext.Default.CommunityStationDto, token, statusSink, onFailure);
    }

    public Task<MyCommunityStationDto?> MineAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/radio/mine", AethernetJsonContext.Default.MyCommunityStationDto, token, null, onFailure);
    }

    public Task<MyCommunityStationDto?> UpdateMineAsync(UpdateCommunityStationRequest request, CancellationToken token,
        Action<int>? statusSink = null, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, "/radio/mine", request,
            AethernetJsonContext.Default.UpdateCommunityStationRequest, AethernetJsonContext.Default.MyCommunityStationDto,
            token, statusSink, onFailure);
    }

    public Task<RadioTrackPage?> TracksAsync(string stationId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/radio/stations/{Uri.EscapeDataString(stationId)}/tracks",
            AethernetJsonContext.Default.RadioTrackPage, token, null, onFailure);
    }

    public Task<RadioFollowResultDto?> FollowAsync(string stationId, bool follow, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var method = follow ? HttpMethod.Post : HttpMethod.Delete;
        return net.RequestAsync(method, $"/radio/stations/{Uri.EscapeDataString(stationId)}/follow",
            AethernetJsonContext.Default.RadioFollowResultDto, token, null, onFailure);
    }
}
