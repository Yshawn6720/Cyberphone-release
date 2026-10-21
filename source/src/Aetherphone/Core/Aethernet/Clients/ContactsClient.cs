using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class ContactsClient
{
    private readonly AethernetTransport net;

    public ContactsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<ContactListResult?> ListAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/contacts/", AethernetJsonContext.Default.ContactListResult, token, null, onFailure);
    }

    public Task<ContactDto?> AddAsync(string number, string? alias, CancellationToken token, Action<int>? statusSink = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/contacts/", new AddContactRequest(number, alias), AethernetJsonContext.Default.AddContactRequest, AethernetJsonContext.Default.ContactDto, token, statusSink, onFailure);
    }

    public Task<ContactDto?> UpdateAliasAsync(string userId, string alias, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/contacts/{Uri.EscapeDataString(userId)}", new UpdateContactAliasRequest(alias), AethernetJsonContext.Default.UpdateContactAliasRequest, AethernetJsonContext.Default.ContactDto, token, null, onFailure);
    }

    public Task<bool> RemoveAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/contacts/{Uri.EscapeDataString(userId)}", token, null, onFailure);
    }

    public Task<NumberChangeStatusResult?> NumberChangeStatusAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/contacts/number-change", AethernetJsonContext.Default.NumberChangeStatusResult, token,
            null, onFailure);
    }

    public Task<NumberChangeStatusResult?> RequestNumberChangeAsync(string reason, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/contacts/number-change", new CreateNumberChangeRequest(reason), AethernetJsonContext.Default.CreateNumberChangeRequest, AethernetJsonContext.Default.NumberChangeStatusResult, token, null, onFailure);
    }
}
