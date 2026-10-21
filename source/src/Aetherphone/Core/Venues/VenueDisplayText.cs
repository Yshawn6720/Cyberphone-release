using System.Text;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Rolladeck;

namespace Aetherphone.Core.Venues;

internal static class VenueDisplayText
{
    public const string Separator = " · ";

    private enum GlyphClass : byte
    {
        Keep,
        Space,
        Separator,
        Drop,
    }

    public static string Clean(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return IsClean(input) ? input : Rebuild(input);
    }

    private static bool IsClean(string input)
    {
        var previousSpace = true;
        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (character == ' ')
            {
                if (previousSpace)
                {
                    return false;
                }

                previousSpace = true;
                continue;
            }

            if (char.IsSurrogate(character) || Classify(character) != GlyphClass.Keep)
            {
                return false;
            }

            previousSpace = false;
        }

        return !previousSpace;
    }

    private static string Rebuild(string input)
    {
        var builder = new StringBuilder(input.Length);
        var pendingSpace = false;
        var pendingSeparator = false;
        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            int codepoint = character;
            if (char.IsHighSurrogate(character) && index + 1 < input.Length && char.IsLowSurrogate(input[index + 1]))
            {
                codepoint = char.ConvertToUtf32(character, input[index + 1]);
                index++;
            }

            var mapped = codepoint > char.MaxValue ? RolladeckText.MapSupplementary(codepoint) : MapLetter(codepoint);
            var glyphClass = mapped != '\0' ? GlyphClass.Keep : Classify(codepoint);
            switch (glyphClass)
            {
                case GlyphClass.Drop:
                    continue;
                case GlyphClass.Space:
                    pendingSpace = true;
                    continue;
                case GlyphClass.Separator:
                    pendingSeparator = true;
                    continue;
            }

            if (builder.Length > 0)
            {
                if (pendingSeparator)
                {
                    builder.Append(Separator);
                }
                else if (pendingSpace)
                {
                    builder.Append(' ');
                }
            }

            pendingSpace = false;
            pendingSeparator = false;
            builder.Append(mapped != '\0' ? mapped : (char)codepoint);
        }

        return builder.ToString();
    }

    private static GlyphClass Classify(int codepoint)
    {
        if (codepoint is >= 0x21 and <= 0x7E)
        {
            return GlyphClass.Keep;
        }

        if (codepoint is ' ' or 0x00A0 or 0x3000 or 0x202F or 0x205F || codepoint is >= 0x2000 and <= 0x200A)
        {
            return GlyphClass.Space;
        }

        if (codepoint < 0x20 || codepoint is >= 0x7F and <= 0x9F)
        {
            return GlyphClass.Space;
        }

        if (codepoint is 0x00AD or 0xFEFF || codepoint is >= 0x200B and <= 0x200F ||
            codepoint is >= 0x2028 and <= 0x202E || codepoint is >= 0x2060 and <= 0x206F ||
            codepoint is >= 0xFE00 and <= 0xFE0F ||
            codepoint is >= 0x20D0 and <= 0x20FF)
        {
            return GlyphClass.Drop;
        }

        if (IsSeparator(codepoint))
        {
            return GlyphClass.Separator;
        }

        if (GlyphPlan.IsGameSymbol(codepoint))
        {
            return GlyphClass.Keep;
        }

        return IsRenderable(codepoint) ? GlyphClass.Keep : GlyphClass.Space;
    }

    private static char MapLetter(int codepoint) =>
        codepoint switch
        {
            >= 0x24B6 and <= 0x24CF => (char)('A' + codepoint - 0x24B6),
            >= 0x24D0 and <= 0x24E9 => (char)('a' + codepoint - 0x24D0),
            0x1D00 => 'A',
            0x0299 => 'B',
            0x1D04 => 'C',
            0x1D05 => 'D',
            0x1D07 => 'E',
            0xA730 => 'F',
            0x0262 => 'G',
            0x029C => 'H',
            0x026A => 'I',
            0x1D0A => 'J',
            0x1D0B => 'K',
            0x029F => 'L',
            0x1D0D => 'M',
            0x0274 => 'N',
            0x1D0F => 'O',
            0x1D18 => 'P',
            0x0280 => 'R',
            0xA731 => 'S',
            0x1D1B => 'T',
            0x1D1C => 'U',
            0x1D20 => 'V',
            0x1D21 => 'W',
            0x028F => 'Y',
            0x1D22 => 'Z',
            _ => '\0',
        };

    private static bool IsSeparator(int codepoint) =>
        codepoint is 0x00B7 or 0x2022 or 0x2023 or 0x2027 or 0x2043 or 0x2219 or 0x22C4 or 0x22C5 or 0x22C6 ||
        codepoint is >= 0x2300 and <= 0x24FF ||
        codepoint is >= 0x2500 and <= 0x27EF ||
        codepoint is >= 0x2980 and <= 0x29FF ||
        codepoint is >= 0x2B00 and <= 0x2BFF ||
        codepoint is >= 0x3200 and <= 0x33FF;

    private static bool IsRenderable(int codepoint) =>
        codepoint is >= 0x00A1 and <= 0x024F ||
        codepoint is >= 0x0300 and <= 0x036F ||
        codepoint is >= 0x0370 and <= 0x052F ||
        codepoint is >= 0x1100 and <= 0x11FF ||
        codepoint is >= 0x1E00 and <= 0x1EFF ||
        codepoint is >= 0x2010 and <= 0x205E ||
        codepoint is >= 0x20A0 and <= 0x20BF ||
        codepoint is >= 0x2100 and <= 0x214F ||
        codepoint is >= 0x2190 and <= 0x22FF ||
        codepoint is >= 0x2E80 and <= 0x2FDF ||
        codepoint is >= 0x3001 and <= 0x31FF ||
        codepoint is >= 0x3400 and <= 0x4DBF ||
        codepoint is >= 0x4E00 and <= 0x9FFF ||
        codepoint is >= 0xAC00 and <= 0xD7AF ||
        codepoint is >= 0xF900 and <= 0xFAFF ||
        codepoint is >= 0xFF00 and <= 0xFFEF;
}
