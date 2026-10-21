using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class GamesClient
{
    internal const string RoomsPath = "/games/rooms";
    internal const string JoinPath = "/games/rooms/join";

    private readonly AethernetTransport net;

    public GamesClient(AethernetTransport net)
    {
        this.net = net;
    }

    internal static string RoomPath(string roomId, string leaf = "")
    {
        return leaf.Length == 0
            ? string.Concat(RoomsPath, "/", Uri.EscapeDataString(roomId))
            : string.Concat(RoomsPath, "/", Uri.EscapeDataString(roomId), "/", leaf);
    }

    public Task<GameRoomListDto?> RoomsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RoomsPath, AethernetJsonContext.Default.GameRoomListDto, token, null, onFailure);
    }

    public Task<GameRoomResultDto?> CreateRoomAsync(string clientRoomId, string gameKind,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(RoomsPath, new GameRoomCreateRequest(clientRoomId, gameKind),
            AethernetJsonContext.Default.GameRoomCreateRequest,
            AethernetJsonContext.Default.GameRoomResultDto, token, null, onFailure);
    }

    public Task<GameRoomResultDto?> JoinByCodeAsync(string code, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(JoinPath, new GameRoomJoinRequest(code),
            AethernetJsonContext.Default.GameRoomJoinRequest,
            AethernetJsonContext.Default.GameRoomResultDto, token, null, onFailure);
    }

    public Task<GameRoomCardDto?> RoomCardAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RoomPath(roomId), AethernetJsonContext.Default.GameRoomCardDto, token, null,
            onFailure);
    }

    public Task<GameRoomSnapshotDto?> RoomStateAsync(string roomId, Action<int> onStatus,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RoomPath(roomId, "state"), AethernetJsonContext.Default.GameRoomSnapshotDto,
            token, onStatus, onFailure);
    }

    public Task<GameRoomYouDto?> YouAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RoomPath(roomId, "you"), AethernetJsonContext.Default.GameRoomYouDto, token,
            null, onFailure);
    }

    public Task<GameRoomActionResultDto?> ActAsync(string roomId, GameRoomActionRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(RoomPath(roomId, "act"), request,
            AethernetJsonContext.Default.GameRoomActionRequest,
            AethernetJsonContext.Default.GameRoomActionResultDto, token, null, onFailure);
    }

    public Task<GameRoomActionDto?> LeaveAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, RoomPath(roomId, "leave"),
            AethernetJsonContext.Default.GameRoomActionDto, token, null, onFailure);
    }

    public Task<GameRoomActionDto?> KickAsync(string roomId, string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(RoomPath(roomId, "kick"), new GameRoomMemberRequest(userId),
            AethernetJsonContext.Default.GameRoomMemberRequest,
            AethernetJsonContext.Default.GameRoomActionDto, token, null, onFailure);
    }

    public Task<GameRoomActionDto?> CloseAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, RoomPath(roomId, "close"),
            AethernetJsonContext.Default.GameRoomActionDto, token, null, onFailure);
    }
}
