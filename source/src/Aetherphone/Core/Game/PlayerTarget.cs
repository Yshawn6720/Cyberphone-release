using System.Text;

namespace Aetherphone.Core.Game;

internal static class PlayerTarget
{
    private const int MaxNameLength = 32;
    private const int MaxWorldLength = 24;
    private const int PrivateUseFirst = 0xE000;
    private const int PrivateUseLast = 0xF8FF;

    public static bool TrySplit(string name, string world, out string playerName, out string worldName)
    {
        playerName = string.Empty;
        worldName = string.Empty;
        var cleanName = Clean(name);
        var cleanWorld = Clean(world);
        var separator = cleanName.IndexOf('@');
        if (separator >= 0)
        {
            if (cleanWorld.Length == 0)
            {
                cleanWorld = Clean(cleanName[(separator + 1)..]);
            }

            cleanName = Clean(cleanName[..separator]);
        }

        if (!IsPlayerName(cleanName))
        {
            return false;
        }

        if (cleanWorld.Length > 0 && !IsWorldName(cleanWorld))
        {
            return false;
        }

        playerName = cleanName;
        worldName = cleanWorld;
        return true;
    }

    public static bool TryFormat(string name, string world, out string target)
    {
        target = string.Empty;
        if (!TrySplit(name, world, out var playerName, out var worldName))
        {
            return false;
        }

        target = worldName.Length > 0 ? string.Concat(playerName, "@", worldName) : playerName;
        return true;
    }

    private static bool IsPlayerName(string value)
    {
        if (value.Length == 0 || value.Length > MaxNameLength)
        {
            return false;
        }

        var letters = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsLetter(character))
            {
                letters++;
                continue;
            }

            if (character != ' ' && character != '\'' && character != '-' && character != '.')
            {
                return false;
            }
        }

        return letters > 0;
    }

    private static bool IsWorldName(string value)
    {
        if (value.Length == 0 || value.Length > MaxWorldLength)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsLetter(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string Clean(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (!NeedsCleaning(value))
        {
            return value.Trim();
        }

        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (IsStripped(character))
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }

    private static bool NeedsCleaning(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (IsStripped(value[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStripped(char character)
    {
        return char.IsControl(character) || (character >= PrivateUseFirst && character <= PrivateUseLast);
    }
}
