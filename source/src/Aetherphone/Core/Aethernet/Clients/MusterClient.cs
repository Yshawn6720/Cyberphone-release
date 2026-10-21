using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class MusterClient
{
    private readonly AethernetTransport net;

    public MusterClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<MusterDto?> CreateAsync(CreateMusterRequest request, CancellationToken token, Action<int>? statusSink = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/musters/", request, AethernetJsonContext.Default.CreateMusterRequest,
            AethernetJsonContext.Default.MusterDto, token, statusSink, onFailure);
    }

    public Task<bool> EndAsync(string musterId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/musters/{Uri.EscapeDataString(musterId)}/end", token, null, onFailure);
    }

    public Task<MusterRsvpResult?> RsvpAsync(string musterId, bool going, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/musters/{Uri.EscapeDataString(musterId)}/rsvp", new SetMusterRsvpRequest(going),
            AethernetJsonContext.Default.SetMusterRsvpRequest, AethernetJsonContext.Default.MusterRsvpResult, token,
            null, onFailure);
    }

    public Task<MusterPage?> DirectoryAsync(int categories, int regions, int dataCenterId, string? cursor,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var path = $"/musters/?categories={categories}&regions={regions}&dc={dataCenterId}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.MusterPage, token, null, onFailure);
    }

    public Task<bool> StatusAsync(string musterId, int status, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/musters/{Uri.EscapeDataString(musterId)}/status",
            new SetMusterStatusRequest(status), AethernetJsonContext.Default.SetMusterStatusRequest, token, null,
            onFailure);
    }

    public Task<bool> NoticeAsync(string musterId, SetMusterNoticeRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/musters/{Uri.EscapeDataString(musterId)}/notice",
            request, AethernetJsonContext.Default.SetMusterNoticeRequest, token, null, onFailure);
    }

    public Task<MusterSync?> SyncAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/musters/sync", AethernetJsonContext.Default.MusterSync, token, null, onFailure);
    }

    public Task<MusterDto?> GetAsync(string musterId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/musters/{Uri.EscapeDataString(musterId)}", AethernetJsonContext.Default.MusterDto, token,
            null, onFailure);
    }
}
