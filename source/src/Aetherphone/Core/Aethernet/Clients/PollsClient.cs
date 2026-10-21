using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class PollsClient
{
    private readonly AethernetTransport net;

    public PollsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<PollPage?> ListAsync(string? cursor, string lang, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var path = $"/polls?lang={Uri.EscapeDataString(lang)}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.PollPage, token, null, onFailure);
    }

    public Task<PollDto?> VoteAsync(string pollId, int option, string lang, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/polls/{pollId}/vote?lang={Uri.EscapeDataString(lang)}", new PollVoteRequest(option), AethernetJsonContext.Default.PollVoteRequest, AethernetJsonContext.Default.PollDto, token, null, onFailure);
    }

    public Task<PollDto?> ClearVoteAsync(string pollId, string lang, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/polls/{pollId}/vote?lang={Uri.EscapeDataString(lang)}", AethernetJsonContext.Default.PollDto, token, null, onFailure);
    }
}
