using System.Text;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.GameChat;

internal sealed class ChatCapture : IDisposable
{
    private const char PrivateUseFirst = '\uE000';
    private const char PrivateUseLast = '\uF8FF';

    private readonly ChatLog log;
    private readonly ChatSend send;
    private readonly IChatGui chatGui;
    private readonly GameData gameData;
    private readonly AppGate installed;
    private readonly List<ChatChunk> chunks = new(8);
    private readonly StringBuilder pendingText = new(256);
    private readonly StringBuilder flatText = new(256);

    public ChatCapture(ChatLog log, ChatSend send, IChatGui chatGui, GameData gameData, AppGate installed)
    {
        this.log = log;
        this.send = send;
        this.chatGui = chatGui;
        this.gameData = gameData;
        this.installed = installed;
        chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose() => chatGui.ChatMessage -= OnChatMessage;

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!installed.Open || !GameChannels.TryResolve(type, out var channel))
        {
            return;
        }

        var built = BuildChunks(message, out var text);
        if (text.Length == 0)
        {
            return;
        }

        ResolveAuthor(type, sender, channel, out var name, out var world, out var self);
        var flags = self ? ChatEntryFlags.Self : ChatEntryFlags.None;
        if (self)
        {
            send.TryResolve(channel.Key, text);
        }
        else if (MentionsLocalPlayer(text))
        {
            flags |= ChatEntryFlags.Mention;
        }

        log.Append(new ChatEntry(log.NextSequence(), channel.Key, name, world, text, built, DateTime.Now, flags));
        if (!isHandled && ChannelStyles.Shared.HidesFromGameChat(channel))
        {
            isHandled = true;
        }
    }

    private void ResolveAuthor(XivChatType kind, SeString sender, GameChannel channel, out string name, out string world,
        out bool self)
    {
        var resolved = TryResolvePlayer(sender, out var senderName, out var senderWorld);
        if (channel.Category == ChannelCategory.Direct)
        {
            self = kind == XivChatType.TellOutgoing;
            name = resolved ? senderName : sender.TextValue;
            world = resolved ? senderWorld : gameData.WorldName(gameData.LocalHomeWorldId);
            return;
        }

        if (!resolved || gameData.IsLocalPlayer(senderName, senderWorld))
        {
            self = true;
            var local = gameData.LocalPlayer;
            name = local is not null ? local.Name.TextValue : senderName;
            world = gameData.WorldName(gameData.LocalHomeWorldId);
            return;
        }

        self = false;
        name = senderName;
        world = senderWorld;
    }

    private bool TryResolvePlayer(SeString sender, out string name, out string world)
    {
        var payloads = sender.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is PlayerPayload player)
            {
                name = player.PlayerName;
                world = gameData.WorldName(player.World.RowId);
                return true;
            }
        }

        name = string.Empty;
        world = string.Empty;
        return false;
    }

    private ChatChunk[] BuildChunks(SeString message, out string text)
    {
        chunks.Clear();
        pendingText.Clear();
        flatText.Clear();
        var link = PendingLink.None;
        var payloads = message.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            switch (payloads[index])
            {
                case PlayerPayload player:
                    FlushText();
                    link = PendingLink.ForPlayer(player.PlayerName, gameData.WorldName(player.World.RowId));
                    break;
                case ItemPayload item:
                    FlushText();
                    link = PendingLink.ForItem(item.ItemId, item.DisplayName ?? string.Empty);
                    break;
                case MapLinkPayload map:
                    FlushText();
                    link = PendingLink.ForMap(map.TerritoryType.RowId, map.Map.RowId, map.RawX, map.RawY,
                        string.Concat(map.PlaceName, " ", map.CoordinateString));
                    break;
                case StatusPayload status:
                    FlushText();
                    link = PendingLink.ForId(ChatChunkKind.Status, status.Status.RowId);
                    break;
                case QuestPayload quest:
                    FlushText();
                    link = PendingLink.ForId(ChatChunkKind.Quest, quest.Quest.RowId);
                    break;
                case PartyFinderPayload partyFinder:
                    FlushText();
                    link = PendingLink.ForId(ChatChunkKind.PartyFinder, partyFinder.ListingId);
                    break;
                case DalamudLinkPayload pluginLink:
                    FlushText();
                    link = PendingLink.ForPlugin(pluginLink.Plugin, pluginLink.CommandId);
                    break;
                case AutoTranslatePayload autoTranslate:
                    AppendAutoTranslate(autoTranslate.Text, ref link);
                    break;
                case ITextProvider provider:
                    AppendText(provider.Text, ref link);
                    break;
            }
        }

        if (link.Active)
        {
            AddLink(link, link.Fallback);
        }

        FlushText();
        text = flatText.ToString().Trim();
        return chunks.Count == 0 ? Array.Empty<ChatChunk>() : chunks.ToArray();
    }

    private void AppendText(string? value, ref PendingLink link)
    {
        var cleaned = Clean(value);
        if (cleaned.Length == 0)
        {
            return;
        }

        if (!link.Active)
        {
            pendingText.Append(cleaned);
            return;
        }

        AddLink(link, cleaned);
        link = PendingLink.None;
    }

    private void AppendAutoTranslate(string? value, ref PendingLink link)
    {
        var cleaned = Clean(value);
        if (cleaned.Length == 0)
        {
            return;
        }

        if (link.Active)
        {
            AddLink(link, cleaned);
            link = PendingLink.None;
            return;
        }

        FlushText();
        chunks.Add(ChatChunk.AutoTranslate(cleaned));
        flatText.Append(cleaned);
    }

    private void AddLink(PendingLink link, string label)
    {
        var text = label.Length > 0 ? label : link.Fallback;
        if (text.Length == 0)
        {
            return;
        }

        chunks.Add(link.Resolve(text));
        flatText.Append(text);
    }

    private void FlushText()
    {
        if (pendingText.Length == 0)
        {
            return;
        }

        var value = pendingText.ToString();
        pendingText.Clear();
        chunks.Add(ChatChunk.Plain(value));
        flatText.Append(value);
    }

    private bool MentionsLocalPlayer(string text)
    {
        var local = gameData.LocalPlayer;
        if (local is null)
        {
            return false;
        }

        var full = local.Name.TextValue;
        if (full.Length == 0)
        {
            return false;
        }

        if (text.Contains(full, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var space = full.IndexOf(' ');
        return space > 0 && ContainsWord(text, full.AsSpan(0, space));
    }

    private static bool ContainsWord(string text, ReadOnlySpan<char> word)
    {
        var start = 0;
        while (start <= text.Length - word.Length)
        {
            var found = text.AsSpan(start).IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return false;
            }

            var at = start + found;
            var openStart = at == 0 || !char.IsLetter(text[at - 1]);
            var after = at + word.Length;
            var openEnd = after >= text.Length || !char.IsLetter(text[after]);
            if (openStart && openEnd)
            {
                return true;
            }

            start = at + 1;
        }

        return false;
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var keep = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (!IsUnsupportedGlyph(value[index]))
            {
                keep++;
            }
        }

        if (keep == value.Length)
        {
            return value;
        }

        if (keep == 0)
        {
            return string.Empty;
        }

        return string.Create(keep, value, static (span, source) =>
        {
            var target = 0;
            for (var index = 0; index < source.Length; index++)
            {
                if (IsUnsupportedGlyph(source[index]))
                {
                    continue;
                }

                span[target++] = source[index];
            }
        });
    }

    private static bool IsUnsupportedGlyph(char value) =>
        value >= PrivateUseFirst && value <= PrivateUseLast && !GlyphPlan.IsGameSymbol(value);

    private readonly struct PendingLink
    {
        public static readonly PendingLink None = default;

        private readonly ChatChunkKind kind;
        private readonly string world;
        private readonly uint itemId;
        private readonly uint territoryId;
        private readonly uint mapId;
        private readonly int rawX;
        private readonly int rawY;

        private PendingLink(ChatChunkKind kind, string world, uint itemId, uint territoryId, uint mapId, int rawX,
            int rawY, string fallback)
        {
            this.kind = kind;
            this.world = world;
            this.itemId = itemId;
            this.territoryId = territoryId;
            this.mapId = mapId;
            this.rawX = rawX;
            this.rawY = rawY;
            Active = true;
            Fallback = fallback;
        }

        public bool Active { get; }

        public string Fallback { get; }

        public static PendingLink ForPlayer(string name, string world) =>
            new(ChatChunkKind.Player, world, 0u, 0u, 0u, 0, 0, name);

        public static PendingLink ForItem(uint itemId, string name) =>
            new(ChatChunkKind.Item, string.Empty, itemId, 0u, 0u, 0, 0, name);

        public static PendingLink ForMap(uint territoryId, uint mapId, int rawX, int rawY, string label) =>
            new(ChatChunkKind.Map, string.Empty, 0u, territoryId, mapId, rawX, rawY, label);

        public static PendingLink ForId(ChatChunkKind kind, uint id) =>
            new(kind, string.Empty, id, 0u, 0u, 0, 0, string.Empty);

        public static PendingLink ForPlugin(string plugin, uint commandId) =>
            new(ChatChunkKind.PluginLink, plugin, commandId, 0u, 0u, 0, 0, string.Empty);

        public ChatChunk Resolve(string text) => kind switch
        {
            ChatChunkKind.Player => ChatChunk.Player(text, world),
            ChatChunkKind.Item => ChatChunk.Item(text, itemId),
            ChatChunkKind.Map => ChatChunk.Map(text, territoryId, mapId, rawX, rawY),
            ChatChunkKind.Status => ChatChunk.Status(text, itemId),
            ChatChunkKind.Quest => ChatChunk.Quest(text, itemId),
            ChatChunkKind.PartyFinder => ChatChunk.PartyFinder(text, itemId),
            ChatChunkKind.PluginLink => ChatChunk.PluginLink(text, world, itemId),
            _ => ChatChunk.Plain(text),
        };
    }
}
