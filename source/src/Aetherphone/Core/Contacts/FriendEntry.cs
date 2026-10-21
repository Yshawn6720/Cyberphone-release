namespace Aetherphone.Core.Contacts;

internal sealed record FriendEntry(
    ulong ContentId,
    string Name,
    string WorldName,
    string FreeCompany,
    string Job,
    string JobName,
    string Location,
    bool Online,
    ushort HomeWorldId,
    ushort CurrentWorldId);
