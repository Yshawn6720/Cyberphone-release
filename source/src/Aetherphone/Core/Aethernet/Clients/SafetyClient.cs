using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Aetherphone.Core.Report;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class SafetyClient
{
    private readonly AethernetTransport net;

    public SafetyClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<bool> ReportAsync(string targetType, string targetId, ReportReason reason, CancellationToken token,
        RevealedMessageDto[]? revealedMessages = null, Action<AepFailure>? onFailure = null)
    {
        var request = new ReportRequest(targetType, targetId, reason.Details, revealedMessages, reason.Category);
        return net.SendJsonForStatusAsync(HttpMethod.Post, "/reports", request, AethernetJsonContext.Default.ReportRequest, token, null, onFailure);
    }

    public Task<bool> BlockAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/blocks/{Uri.EscapeDataString(userId)}", token, null, onFailure);
    }

    public Task<bool> UnblockAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/blocks/{Uri.EscapeDataString(userId)}", token, null, onFailure);
    }

    public Task<UserSearchResult?> BlockedUsersAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/blocks/", AethernetJsonContext.Default.UserSearchResult, token, null, onFailure);
    }
}
