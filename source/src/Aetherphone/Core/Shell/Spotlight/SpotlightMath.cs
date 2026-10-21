using System.Globalization;

namespace Aetherphone.Core.Shell.Spotlight;

internal static class SpotlightMath
{
    private const string ResultFormat = "0.####";

    public static bool TryEvaluate(string text, out string formatted)
    {
        formatted = string.Empty;
        if (!LooksLikeExpression(text))
        {
            return false;
        }

        var parser = new Parser(text);
        if (!parser.TryParseExpression(out var value))
        {
            return false;
        }

        parser.SkipSpace();
        if (!parser.AtEnd || double.IsNaN(value) || double.IsInfinity(value))
        {
            return false;
        }

        formatted = value.ToString(ResultFormat, CultureInfo.InvariantCulture);
        return true;
    }

    private static bool LooksLikeExpression(string text)
    {
        var hasDigit = false;
        var hasOperator = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsDigit(character))
            {
                hasDigit = true;
                continue;
            }

            if (character is '+' or '-' or '*' or 'x' or 'X' or '/' or '%')
            {
                hasOperator = true;
                continue;
            }

            if (character is ' ' or '.' or '(' or ')')
            {
                continue;
            }

            return false;
        }

        return hasDigit && hasOperator;
    }

    private ref struct Parser
    {
        private readonly ReadOnlySpan<char> text;
        private int position;

        public Parser(ReadOnlySpan<char> text)
        {
            this.text = text;
            position = 0;
        }

        public readonly bool AtEnd => position >= text.Length;

        public void SkipSpace()
        {
            while (position < text.Length && text[position] == ' ')
            {
                position++;
            }
        }

        public bool TryParseExpression(out double value)
        {
            if (!TryParseTerm(out value))
            {
                return false;
            }

            while (true)
            {
                SkipSpace();
                if (AtEnd)
                {
                    return true;
                }

                var op = text[position];
                if (op != '+' && op != '-')
                {
                    return true;
                }

                position++;
                if (!TryParseTerm(out var right))
                {
                    return false;
                }

                value = op == '+' ? value + right : value - right;
            }
        }

        private bool TryParseTerm(out double value)
        {
            if (!TryParseFactor(out value))
            {
                return false;
            }

            while (true)
            {
                SkipSpace();
                if (AtEnd)
                {
                    return true;
                }

                var op = text[position];
                if (op is not ('*' or 'x' or 'X' or '/' or '%'))
                {
                    return true;
                }

                position++;
                if (!TryParseFactor(out var right))
                {
                    return false;
                }

                if (op is '/' or '%')
                {
                    if (right == 0d)
                    {
                        return false;
                    }

                    value = op == '/' ? value / right : value % right;
                    continue;
                }

                value *= right;
            }
        }

        private bool TryParseFactor(out double value)
        {
            value = 0d;
            var sign = 1d;
            SkipSpace();
            while (!AtEnd && text[position] is '+' or '-')
            {
                if (text[position] == '-')
                {
                    sign = -sign;
                }

                position++;
                SkipSpace();
            }

            if (AtEnd)
            {
                return false;
            }

            if (text[position] == '(')
            {
                position++;
                if (!TryParseExpression(out var inner))
                {
                    return false;
                }

                SkipSpace();
                if (AtEnd || text[position] != ')')
                {
                    return false;
                }

                position++;
                value = sign * inner;
                return true;
            }

            var start = position;
            while (!AtEnd && (char.IsDigit(text[position]) || text[position] == '.'))
            {
                position++;
            }

            if (position == start ||
                !double.TryParse(text[start..position], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            value = sign * number;
            return true;
        }
    }
}
