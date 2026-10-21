using System.Globalization;
using System.Text;

namespace Aetherphone.Apps.Calculator;

internal readonly record struct NumberMarks(char Decimal, char Group)
{
    public static readonly NumberMarks Invariant = new('.', ',');

    public static NumberMarks From(NumberFormatInfo format)
    {
        var decimalMark = format.NumberDecimalSeparator.Length > 0 ? format.NumberDecimalSeparator[0] : '.';
        var groupMark = format.NumberGroupSeparator.Length > 0 ? format.NumberGroupSeparator[0] : ',';
        if (char.IsWhiteSpace(groupMark))
        {
            groupMark = ' ';
        }

        if (groupMark == decimalMark)
        {
            groupMark = decimalMark == ',' ? '.' : ',';
        }

        return new NumberMarks(decimalMark, groupMark);
    }
}

internal static class CalculatorText
{
    private const int GroupSize = 3;
    private const int MaxPasteLength = 64;
    private const char MinusSign = (char)0x2212;
    private const char Apostrophe = (char)0x27;
    private const char NoBreakSpace = (char)0xA0;
    private const char NarrowNoBreakSpace = (char)0x202F;
    private const char ThinSpace = (char)0x2009;

    public static string Localize(string raw, NumberMarks marks)
    {
        var builder = new StringBuilder(raw.Length + raw.Length / GroupSize + 2);
        var index = 0;
        while (index < raw.Length)
        {
            if (!char.IsAsciiDigit(raw[index]))
            {
                builder.Append(raw[index]);
                index++;
                continue;
            }

            var start = index;
            while (index < raw.Length && char.IsAsciiDigit(raw[index]))
            {
                index++;
            }

            AppendGrouped(builder, raw, start, index - start, marks.Group);
            if (index < raw.Length && raw[index] == '.')
            {
                builder.Append(marks.Decimal);
                index = AppendDigits(builder, raw, index + 1);
            }

            if (index < raw.Length && raw[index] == 'e')
            {
                builder.Append('e');
                index++;
                if (index < raw.Length && (raw[index] == '-' || raw[index] == '+'))
                {
                    builder.Append(raw[index]);
                    index++;
                }

                index = AppendDigits(builder, raw, index);
            }
        }

        return builder.ToString();
    }

    public static string ForClipboard(string raw, NumberMarks marks) =>
        marks.Decimal == '.' ? raw : raw.Replace('.', marks.Decimal);

    public static bool TryParsePasted(string? text, NumberMarks marks, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxPasteLength)
        {
            return false;
        }

        var builder = new StringBuilder(trimmed.Length);
        var negative = false;
        var digits = 0;
        var seenDecimal = false;
        var seenExponent = false;
        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];
            if (char.IsAsciiDigit(character))
            {
                builder.Append(character);
                digits++;
                continue;
            }

            if (builder.Length == 0 && !negative && (character == '-' || character == MinusSign))
            {
                negative = true;
                continue;
            }

            if (builder.Length == 0 && character == '+')
            {
                continue;
            }

            if ((character == 'e' || character == 'E') && digits > 0 && !seenExponent)
            {
                seenExponent = true;
                builder.Append('e');
                if (index + 1 < trimmed.Length && (trimmed[index + 1] == '-' || trimmed[index + 1] == '+'))
                {
                    index++;
                    builder.Append(trimmed[index]);
                }

                continue;
            }

            if (character == marks.Decimal && !seenExponent)
            {
                if (seenDecimal)
                {
                    return false;
                }

                seenDecimal = true;
                builder.Append('.');
                continue;
            }

            if (IsSpacingMark(character) && !seenDecimal && !seenExponent)
            {
                continue;
            }

            if ((character == '.' || character == ',') && !seenDecimal && !seenExponent)
            {
                if (DigitsAfter(trimmed, index + 1) == GroupSize)
                {
                    continue;
                }

                seenDecimal = true;
                builder.Append('.');
                continue;
            }

            return false;
        }

        if (digits == 0)
        {
            return false;
        }

        if (!CalculatorEngine.TryParse(builder.ToString(), out value))
        {
            return false;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }

    private static bool IsSpacingMark(char character) =>
        character is ' ' or Apostrophe or '_' or NoBreakSpace or NarrowNoBreakSpace or ThinSpace;

    private static int DigitsAfter(string text, int index)
    {
        var count = 0;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            count++;
            index++;
        }

        return count;
    }

    private static void AppendGrouped(StringBuilder builder, string raw, int start, int length, char group)
    {
        for (var offset = 0; offset < length; offset++)
        {
            if (offset > 0 && (length - offset) % GroupSize == 0)
            {
                builder.Append(group);
            }

            builder.Append(raw[start + offset]);
        }
    }

    private static int AppendDigits(StringBuilder builder, string raw, int index)
    {
        while (index < raw.Length && char.IsAsciiDigit(raw[index]))
        {
            builder.Append(raw[index]);
            index++;
        }

        return index;
    }
}
