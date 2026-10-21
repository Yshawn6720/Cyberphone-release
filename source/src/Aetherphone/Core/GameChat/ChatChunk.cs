namespace Aetherphone.Core.GameChat;

internal enum ChatChunkKind : byte
{
    Text,
    AutoTranslate,
    Player,
    Item,
    Map,
    Status,
    Quest,
    PartyFinder,
    PluginLink,
    Url,
}

internal readonly struct ChatChunk
{
    public readonly ChatChunkKind Kind;
    public readonly string Text;
    public readonly string World;
    public readonly string Plugin;
    public readonly uint Id;
    public readonly uint TerritoryId;
    public readonly uint MapId;
    public readonly int RawX;
    public readonly int RawY;

    private ChatChunk(ChatChunkKind kind, string text, string world, string plugin, uint id, uint territoryId,
        uint mapId, int rawX, int rawY)
    {
        Kind = kind;
        Text = text;
        World = world;
        Plugin = plugin;
        Id = id;
        TerritoryId = territoryId;
        MapId = mapId;
        RawX = rawX;
        RawY = rawY;
    }

    public static ChatChunk Plain(string text) =>
        new(ChatChunkKind.Text, text, string.Empty, string.Empty, 0u, 0u, 0u, 0, 0);

    public static ChatChunk AutoTranslate(string text) =>
        new(ChatChunkKind.AutoTranslate, text, string.Empty, string.Empty, 0u, 0u, 0u, 0, 0);

    public static ChatChunk Player(string name, string world) =>
        new(ChatChunkKind.Player, name, world, string.Empty, 0u, 0u, 0u, 0, 0);

    public static ChatChunk Item(string name, uint itemId) =>
        new(ChatChunkKind.Item, name, string.Empty, string.Empty, itemId, 0u, 0u, 0, 0);

    public static ChatChunk Map(string text, uint territoryId, uint mapId, int rawX, int rawY) =>
        new(ChatChunkKind.Map, text, string.Empty, string.Empty, 0u, territoryId, mapId, rawX, rawY);

    public static ChatChunk Status(string text, uint statusId) =>
        new(ChatChunkKind.Status, text, string.Empty, string.Empty, statusId, 0u, 0u, 0, 0);

    public static ChatChunk Quest(string text, uint questId) =>
        new(ChatChunkKind.Quest, text, string.Empty, string.Empty, questId, 0u, 0u, 0, 0);

    public static ChatChunk PartyFinder(string text, uint listingId) =>
        new(ChatChunkKind.PartyFinder, text, string.Empty, string.Empty, listingId, 0u, 0u, 0, 0);

    public static ChatChunk PluginLink(string text, string plugin, uint commandId) =>
        new(ChatChunkKind.PluginLink, text, string.Empty, plugin, commandId, 0u, 0u, 0, 0);

    public static ChatChunk Url(string text) =>
        new(ChatChunkKind.Url, text, string.Empty, string.Empty, 0u, 0u, 0u, 0, 0);

    public bool IsPlainText => Kind == ChatChunkKind.Text;

    public bool IsLink => Kind is not (ChatChunkKind.Text or ChatChunkKind.AutoTranslate);
}
