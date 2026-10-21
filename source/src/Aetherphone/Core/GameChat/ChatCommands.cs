using System.Collections.Frozen;

namespace Aetherphone.Core.GameChat;

internal static class ChatCommands
{
    private static readonly FrozenDictionary<string, string> Aliases = BuildAliases();

    public static bool TryResolve(string token, out GameChannel channel)
    {
        channel = null!;
        return token.Length > 0 && Aliases.TryGetValue(token, out var key) && GameChannels.TryByKey(key, out channel);
    }

    public static bool TryAbsorb(string draft, out GameChannel channel, out string remainder)
    {
        channel = null!;
        remainder = draft;
        if (draft.Length < 2 || draft[0] != '/')
        {
            return false;
        }

        var space = draft.IndexOf(' ');
        if (space < 0)
        {
            return false;
        }

        var token = draft[1..space];
        if (!TryResolve(token, out channel))
        {
            return false;
        }

        remainder = draft[(space + 1)..].TrimStart();
        return true;
    }

    private static FrozenDictionary<string, string> BuildAliases()
    {
        var map = new Dictionary<string, string>(64, StringComparer.OrdinalIgnoreCase);
        var all = GameChannels.All;
        for (var index = 0; index < all.Length; index++)
        {
            var channel = all[index];
            if (!channel.CanSend)
            {
                continue;
            }

            map[channel.Command[1..]] = channel.Key;
        }

        map["s"] = GameChannels.SayKey;
        map["sh"] = "shout";
        map["y"] = "yell";
        map["p"] = GameChannels.PartyKey;
        map["a"] = GameChannels.AllianceKey;
        map["fc"] = GameChannels.FreeCompanyKey;
        map["f"] = GameChannels.FreeCompanyKey;
        map["n"] = GameChannels.NoviceKey;
        map["beginner"] = GameChannels.NoviceKey;
        map["t"] = GameChannels.TellKey;
        map["e"] = GameChannels.EchoKey;
        map["pt"] = "pvpteam";
        for (var slot = 1; slot <= GameChannels.LinkshellSlots; slot++)
        {
            map[$"l{slot}"] = $"ls{slot}";
            map[$"ls{slot}"] = $"ls{slot}";
            map[$"cwl{slot}"] = $"cwls{slot}";
            map[$"cwls{slot}"] = $"cwls{slot}";
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
