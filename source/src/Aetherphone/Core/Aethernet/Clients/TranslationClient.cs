using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class TranslationClient
{
    private readonly AethernetTransport net;

    public TranslationClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<TranslateStatusResponse?> StatusAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/translate/status", AethernetJsonContext.Default.TranslateStatusResponse, token, null, onFailure);
    }

    public Task<TranslateBatchResponse?> TranslateAsync(TranslateBatchRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/translate/batch", request, AethernetJsonContext.Default.TranslateBatchRequest,
            AethernetJsonContext.Default.TranslateBatchResponse, token, null, onFailure);
    }
}
