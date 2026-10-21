using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class MediaClient
{
    private readonly AethernetTransport net;

    public MediaClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<UploadUrlResponse?> UploadUrlAsync(string contentType, string scope, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/media/upload-url", new UploadUrlRequest(contentType, scope), AethernetJsonContext.Default.UploadUrlRequest, AethernetJsonContext.Default.UploadUrlResponse, token, null, onFailure);
    }

    public Task<bool> UploadImageAsync(string uploadUrl, byte[] bytes, string contentType, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PutBytesAsync(new Uri(uploadUrl), bytes, contentType, token, onFailure);
    }

    public Task<byte[]?> DownloadAsync(Uri uri, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetBytesAsync(uri, token, onFailure);
    }
}
