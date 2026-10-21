using System.Globalization;
using Aetherphone.Core.Calculator;

namespace Aetherphone.Apps.Calculator;

internal enum CalcOp : byte
{
    None,
    Add,
    Subtract,
    Multiply,
    Divide,
}

internal sealed class CalculatorEngine
{
    public const int MaxHistory = 100;
    private const int MaxDigits = 9;
    private const int RoundingDecimals = 8;
    private const double ScientificAbove = 1e15;
    private const double ScientificBelow = 1e-8;
    private const string Zero = "0";
    private const string NegativeZero = "-0";

    private readonly List<CalculatorHistoryRecord> history;
    private readonly Func<long> clock;
    private readonly List<double> operands = new();
    private readonly List<CalcOp> operators = new();
    private CalcOp repeatOperator;
    private double repeatOperand;
    private bool freshEntry = true;
    private bool entryLocked;
    private bool justEvaluated;
    private string expressionPrefix = string.Empty;
    private string lastExpression = string.Empty;
    private string expression = string.Empty;
    private string? expressionSourcePrefix;
    private string? expressionSourceDisplay;
    private string? expressionSourceLast;
    private bool expressionSourceFresh;
    private bool expressionSourceEvaluated;

    public CalculatorEngine() : this(new List<CalculatorHistoryRecord>(), UnixNow)
    {
    }

    public CalculatorEngine(List<CalculatorHistoryRecord> history, Func<long> clock)
    {
        this.history = history;
        this.clock = clock;
        TrimHistory();
    }

    public string Display { get; private set; } = Zero;

    public bool IsError { get; private set; }

    public IReadOnlyList<CalculatorHistoryRecord> History => history;

    public int SolvedCount { get; private set; }

    public int HistoryVersion { get; private set; }

    public CalcOp ActiveOperator =>
        operators.Count > 0 && freshEntry && !justEvaluated && !IsError ? operators[^1] : CalcOp.None;

    public bool CanBackspace => !IsError && !freshEntry && !entryLocked && !justEvaluated && Display != Zero;

    public string Expression
    {
        get
        {
            if (ReferenceEquals(expressionSourcePrefix, expressionPrefix) &&
                ReferenceEquals(expressionSourceDisplay, Display) &&
                ReferenceEquals(expressionSourceLast, lastExpression) && expressionSourceFresh == freshEntry &&
                expressionSourceEvaluated == justEvaluated)
            {
                return expression;
            }

            expressionSourcePrefix = expressionPrefix;
            expressionSourceDisplay = Display;
            expressionSourceLast = lastExpression;
            expressionSourceFresh = freshEntry;
            expressionSourceEvaluated = justEvaluated;
            expression = BuildExpression();
            return expression;
        }
    }

    private string BuildExpression()
    {
        if (justEvaluated)
        {
            return lastExpression.Length > 0 ? lastExpression + " =" : string.Empty;
        }

        if (expressionPrefix.Length == 0)
        {
            return string.Empty;
        }

        return freshEntry ? expressionPrefix.TrimEnd() : expressionPrefix + Display;
    }

    public void InputDigit(int digit)
    {
        if (digit is < 0 or > 9)
        {
            return;
        }

        PrepareForInput();
        var text = digit.ToString(CultureInfo.InvariantCulture);
        if (freshEntry || entryLocked)
        {
            Display = text;
            freshEntry = false;
            entryLocked = false;
            return;
        }

        if (Display == Zero)
        {
            Display = text;
            return;
        }

        if (Display == NegativeZero)
        {
            Display = "-" + text;
            return;
        }

        if (SignificantDigits(Display) >= MaxDigits)
        {
            return;
        }

        Display += text;
    }

    public void InputDecimal()
    {
        PrepareForInput();
        if (freshEntry || entryLocked)
        {
            Display = "0.";
            freshEntry = false;
            entryLocked = false;
            return;
        }

        if (Display.Contains('.'))
        {
            return;
        }

        if (SignificantDigits(Display) >= MaxDigits)
        {
            return;
        }

        Display += ".";
    }

    public void SetOperator(CalcOp op)
    {
        if (IsError || op == CalcOp.None)
        {
            return;
        }

        if (justEvaluated)
        {
            justEvaluated = false;
            lastExpression = string.Empty;
            ClearTerms();
        }
        else if (freshEntry && operators.Count > 0)
        {
            operators[^1] = op;
            expressionPrefix = ReplaceTrailingOperator(expressionPrefix, op);
            ShowPartial(op);
            return;
        }

        var entryText = Display;
        operands.Add(Parse(entryText));
        operators.Add(op);
        expressionPrefix += entryText + " " + Symbol(op) + " ";
        freshEntry = true;
        entryLocked = false;
        ShowPartial(op);
    }

    public void Equals()
    {
        if (IsError)
        {
            return;
        }

        if (justEvaluated)
        {
            RepeatLast();
            return;
        }

        if (operators.Count == 0)
        {
            freshEntry = false;
            entryLocked = true;
            return;
        }

        var entryText = Display;
        var entry = Parse(entryText);
        operands.Add(entry);
        repeatOperator = operators[^1];
        repeatOperand = entry;
        if (!TryFold(operands.Count, out var sum, out var sumOperator, out var term))
        {
            Fail();
            return;
        }

        Commit(expressionPrefix + entryText, Combine(sum, sumOperator, term));
    }

    public void Negate()
    {
        if (IsError)
        {
            return;
        }

        if (justEvaluated)
        {
            var value = -Parse(Display);
            StartFreshAfterEvaluation();
            Display = Format(value);
            freshEntry = false;
            entryLocked = true;
            return;
        }

        if (freshEntry)
        {
            Display = NegativeZero;
            freshEntry = false;
            entryLocked = false;
            return;
        }

        Display = Display.StartsWith('-') ? Display.Substring(1) : "-" + Display;
    }

    public void Percent()
    {
        if (IsError)
        {
            return;
        }

        if (justEvaluated)
        {
            var result = Parse(Display);
            StartFreshAfterEvaluation();
            Lock(result / 100.0);
            return;
        }

        var entry = Parse(Display);
        if (operators.Count > 0 && operators[^1] is CalcOp.Add or CalcOp.Subtract)
        {
            if (!TryFold(operands.Count, out var sum, out var sumOperator, out var term))
            {
                Fail();
                return;
            }

            Lock(Combine(sum, sumOperator, term) * entry / 100.0);
            return;
        }

        Lock(entry / 100.0);
    }

    public void Backspace()
    {
        if (!CanBackspace)
        {
            return;
        }

        var trimmed = Display.Substring(0, Display.Length - 1);
        Display = trimmed.Length == 0 || trimmed == "-" ? Zero : trimmed;
    }

    public void AllClear()
    {
        ClearTerms();
        repeatOperator = CalcOp.None;
        repeatOperand = 0.0;
        Display = Zero;
        lastExpression = string.Empty;
        freshEntry = true;
        entryLocked = false;
        justEvaluated = false;
        IsError = false;
    }

    public void Enter(double value)
    {
        if (!double.IsFinite(value))
        {
            return;
        }

        PrepareForInput();
        Lock(value);
    }

    public bool Recall(CalculatorHistoryRecord record)
    {
        if (!TryParse(record.Result, out var value))
        {
            return false;
        }

        Enter(value);
        return true;
    }

    public void Remove(CalculatorHistoryRecord record)
    {
        if (history.Remove(record))
        {
            HistoryVersion++;
        }
    }

    public void ClearHistory()
    {
        if (history.Count == 0)
        {
            return;
        }

        history.Clear();
        HistoryVersion++;
    }

    public static string Format(double value)
    {
        var magnitude = Math.Abs(value);
        if (magnitude >= ScientificAbove || (magnitude > 0.0 && magnitude < ScientificBelow))
        {
            return value.ToString("0.########e0", CultureInfo.InvariantCulture);
        }

        var rounded = Math.Round(value, RoundingDecimals, MidpointRounding.AwayFromZero);
        if (rounded == 0.0)
        {
            return Zero;
        }

        return rounded.ToString("0.########", CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private void PrepareForInput()
    {
        if (IsError)
        {
            AllClear();
        }

        StartFreshAfterEvaluation();
    }

    private void Lock(double value)
    {
        if (!double.IsFinite(value))
        {
            Fail();
            return;
        }

        Display = Format(value);
        freshEntry = false;
        entryLocked = true;
    }

    private void ShowPartial(CalcOp op)
    {
        if (!TryFold(operands.Count, out var sum, out var sumOperator, out var term))
        {
            Fail();
            return;
        }

        Display = Format(op is CalcOp.Multiply or CalcOp.Divide ? term : Combine(sum, sumOperator, term));
    }

    private void RepeatLast()
    {
        if (repeatOperator == CalcOp.None)
        {
            return;
        }

        var current = Display;
        if (!TryApply(Parse(current), repeatOperator, repeatOperand, out var result))
        {
            Fail();
            return;
        }

        Commit(current + " " + Symbol(repeatOperator) + " " + Format(repeatOperand), result);
    }

    private void Commit(string expression, double result)
    {
        if (!double.IsFinite(result))
        {
            Fail();
            return;
        }

        Display = Format(result);
        lastExpression = expression;
        PushHistory(expression, Display);
        ClearTerms();
        freshEntry = true;
        entryLocked = false;
        justEvaluated = true;
    }

    private void Fail()
    {
        ClearTerms();
        repeatOperator = CalcOp.None;
        Display = Zero;
        lastExpression = string.Empty;
        freshEntry = true;
        entryLocked = false;
        justEvaluated = false;
        IsError = true;
    }

    private void ClearTerms()
    {
        operands.Clear();
        operators.Clear();
        expressionPrefix = string.Empty;
    }

    private void StartFreshAfterEvaluation()
    {
        if (!justEvaluated)
        {
            return;
        }

        ClearTerms();
        lastExpression = string.Empty;
        justEvaluated = false;
        freshEntry = true;
        entryLocked = false;
    }

    private bool TryFold(int count, out double sum, out CalcOp sumOperator, out double term)
    {
        sum = 0.0;
        sumOperator = CalcOp.Add;
        term = count > 0 ? operands[0] : 0.0;
        for (var index = 1; index < count; index++)
        {
            var op = operators[index - 1];
            var right = operands[index];
            if (op is CalcOp.Multiply or CalcOp.Divide)
            {
                if (!TryApply(term, op, right, out term))
                {
                    return false;
                }

                continue;
            }

            sum = Combine(sum, sumOperator, term);
            sumOperator = op;
            term = right;
        }

        return double.IsFinite(sum) && double.IsFinite(term);
    }

    private static double Combine(double sum, CalcOp op, double term) =>
        op == CalcOp.Subtract ? sum - term : sum + term;

    private static bool TryApply(double left, CalcOp op, double right, out double result)
    {
        switch (op)
        {
            case CalcOp.Add:
                result = left + right;
                break;
            case CalcOp.Subtract:
                result = left - right;
                break;
            case CalcOp.Multiply:
                result = left * right;
                break;
            case CalcOp.Divide:
                if (right == 0.0)
                {
                    result = 0.0;
                    return false;
                }

                result = left / right;
                break;
            default:
                result = right;
                break;
        }

        return double.IsFinite(result);
    }

    private void PushHistory(string expression, string result)
    {
        history.Insert(0, new CalculatorHistoryRecord
        {
            Expression = expression,
            Result = result,
            SolvedAtUnix = clock(),
        });
        TrimHistory();
        SolvedCount++;
        HistoryVersion++;
    }

    private void TrimHistory()
    {
        if (history.Count > MaxHistory)
        {
            history.RemoveRange(MaxHistory, history.Count - MaxHistory);
        }
    }

    private static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static string Symbol(CalcOp op)
    {
        return op switch
        {
            CalcOp.Add => "+",
            CalcOp.Subtract => "-",
            CalcOp.Multiply => "×",
            CalcOp.Divide => "÷",
            _ => string.Empty,
        };
    }

    private static string ReplaceTrailingOperator(string prefix, CalcOp op)
    {
        var trimmed = prefix.TrimEnd();
        var lastSpace = trimmed.LastIndexOf(' ');
        var head = lastSpace >= 0 ? trimmed.Substring(0, lastSpace) : trimmed;
        return head + " " + Symbol(op) + " ";
    }

    private static double Parse(string text) => TryParse(text, out var value) ? value : 0.0;

    private static int SignificantDigits(string text)
    {
        var count = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsAsciiDigit(text[index]))
            {
                count++;
            }
        }

        return count;
    }
}
