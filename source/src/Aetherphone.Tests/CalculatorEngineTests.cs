using Aetherphone.Apps.Calculator;
using Aetherphone.Core.Calculator;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CalculatorEngineTests
{
    private const long SolvedAt = 1_790_000_000;

    private static CalculatorEngine NewEngine() => new(new List<CalculatorHistoryRecord>(), () => SolvedAt);

    private static void Type(CalculatorEngine engine, string keys)
    {
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            switch (key)
            {
                case '+':
                    engine.SetOperator(CalcOp.Add);
                    break;
                case '-':
                    engine.SetOperator(CalcOp.Subtract);
                    break;
                case '*':
                    engine.SetOperator(CalcOp.Multiply);
                    break;
                case '/':
                    engine.SetOperator(CalcOp.Divide);
                    break;
                case '=':
                    engine.Equals();
                    break;
                case '.':
                    engine.InputDecimal();
                    break;
                case '%':
                    engine.Percent();
                    break;
                case 'n':
                    engine.Negate();
                    break;
                case '<':
                    engine.Backspace();
                    break;
                default:
                    engine.InputDigit(key - '0');
                    break;
            }
        }
    }

    [Theory]
    [InlineData("2+3=", "5")]
    [InlineData("9-12=", "-3")]
    [InlineData("6*7=", "42")]
    [InlineData("1/4=", "0.25")]
    [InlineData("2+3*4=", "14")]
    [InlineData("2*3+4*5=", "26")]
    [InlineData("20-6/3=", "18")]
    [InlineData("1/3=", "0.33333333")]
    [InlineData(".1+.2=", "0.3")]
    [InlineData("5+=", "10")]
    [InlineData("5+*2=", "10")]
    [InlineData("2+3==", "8")]
    [InlineData("50%", "0.5")]
    [InlineData("200+10%", "20")]
    [InlineData("200+10%=", "220")]
    [InlineData("200-10%=", "180")]
    [InlineData("50*10%=", "5")]
    [InlineData("5+n3=", "2")]
    [InlineData("7n", "-7")]
    [InlineData("2+3=n", "-5")]
    [InlineData("123<", "12")]
    [InlineData("5<", "0")]
    [InlineData("1.<", "1")]
    [InlineData("1234567891", "123456789")]
    [InlineData("007", "7")]
    [InlineData("1..5", "1.5")]
    public void KeySequencesShowTheExpectedDisplay(string keys, string expected)
    {
        var engine = NewEngine();

        Type(engine, keys);

        Assert.Equal(expected, engine.Display);
    }

    [Fact]
    public void OperatorsShowTheRunningValueWithPrecedence()
    {
        var engine = NewEngine();

        Type(engine, "2+3*");
        Assert.Equal("3", engine.Display);

        Type(engine, "4+");
        Assert.Equal("14", engine.Display);
    }

    [Fact]
    public void ExpressionLineFollowsTheSum()
    {
        var engine = NewEngine();

        Type(engine, "12+");
        Assert.Equal("12 +", engine.Expression);

        Type(engine, "3");
        Assert.Equal("12 + 3", engine.Expression);

        Type(engine, "=");
        Assert.Equal("12 + 3 =", engine.Expression);
    }

    [Fact]
    public void ExpressionIsReusedWhileNothingChanges()
    {
        var engine = NewEngine();
        Type(engine, "12+3");

        var first = engine.Expression;

        Assert.Same(first, engine.Expression);
        Type(engine, "4");
        Assert.Equal("12 + 34", engine.Expression);
    }

    [Fact]
    public void ChangingTheOperatorRewritesTheExpression()
    {
        var engine = NewEngine();

        Type(engine, "5+*");

        Assert.Equal("5 ×", engine.Expression);
        Assert.Equal(CalcOp.Multiply, engine.ActiveOperator);
    }

    [Fact]
    public void ActiveOperatorClearsOnceTheNextNumberStarts()
    {
        var engine = NewEngine();

        Type(engine, "5+");
        Assert.Equal(CalcOp.Add, engine.ActiveOperator);

        Type(engine, "2");
        Assert.Equal(CalcOp.None, engine.ActiveOperator);
    }

    [Fact]
    public void DividingByZeroIsAnErrorUntilTheNextInput()
    {
        var engine = NewEngine();

        Type(engine, "5/0=");
        Assert.True(engine.IsError);
        Assert.Empty(engine.History);

        Type(engine, "+");
        Assert.True(engine.IsError);

        Type(engine, "4");
        Assert.False(engine.IsError);
        Assert.Equal("4", engine.Display);
    }

    [Fact]
    public void DividingByZeroMidChainIsAnError()
    {
        var engine = NewEngine();

        Type(engine, "5/0+");

        Assert.True(engine.IsError);
    }

    [Fact]
    public void AllClearResetsEverything()
    {
        var engine = NewEngine();
        Type(engine, "5+3");

        engine.AllClear();
        Type(engine, "=");

        Assert.Equal("0", engine.Display);
        Assert.Equal(string.Empty, engine.Expression);
        Assert.Empty(engine.History);
    }

    [Fact]
    public void BackspaceOnlyWorksWhileTyping()
    {
        var engine = NewEngine();

        Type(engine, "12");
        Assert.True(engine.CanBackspace);

        Type(engine, "+");
        Assert.False(engine.CanBackspace);

        Type(engine, "3=");
        Assert.False(engine.CanBackspace);
        Type(engine, "<");
        Assert.Equal("15", engine.Display);
    }

    [Fact]
    public void TypingAfterAnAnswerStartsANewSum()
    {
        var engine = NewEngine();

        Type(engine, "2+3=7");

        Assert.Equal("7", engine.Display);
        Assert.Equal(string.Empty, engine.Expression);
    }

    [Fact]
    public void AnOperatorAfterAnAnswerCarriesItForward()
    {
        var engine = NewEngine();

        Type(engine, "2+3=*2=");

        Assert.Equal("10", engine.Display);
        Assert.Equal("5 × 2", engine.History[0].Expression);
    }

    [Fact]
    public void RecallUsesTheAnswerAsTheNextOperand()
    {
        var engine = NewEngine();
        Type(engine, "3+4=");
        var record = engine.History[0];

        Type(engine, "5+");
        Assert.True(engine.Recall(record));
        Type(engine, "=");

        Assert.Equal("12", engine.Display);
    }

    [Fact]
    public void TypingAfterARecallReplacesIt()
    {
        var engine = NewEngine();
        Type(engine, "3+4=");

        engine.Recall(engine.History[0]);
        Type(engine, "9");

        Assert.Equal("9", engine.Display);
    }

    [Fact]
    public void EnteredValuesAreLockedLikeAnAnswer()
    {
        var engine = NewEngine();

        Type(engine, "10*");
        engine.Enter(2.5);
        Assert.False(engine.CanBackspace);
        Type(engine, "=");

        Assert.Equal("25", engine.Display);
    }

    [Fact]
    public void HistoryKeepsNewestFirstWithTheSolveTime()
    {
        var engine = NewEngine();

        Type(engine, "1+1=2+2=");

        Assert.Equal(2, engine.History.Count);
        Assert.Equal("2 + 2", engine.History[0].Expression);
        Assert.Equal("4", engine.History[0].Result);
        Assert.Equal(SolvedAt, engine.History[0].SolvedAtUnix);
        Assert.Equal(2, engine.SolvedCount);
    }

    [Fact]
    public void HistoryIsCappedAndTrimsWhatWasPersisted()
    {
        var persisted = new List<CalculatorHistoryRecord>();
        for (var index = 0; index < CalculatorEngine.MaxHistory + 20; index++)
        {
            persisted.Add(new CalculatorHistoryRecord { Expression = "1 + 1", Result = "2" });
        }

        var engine = new CalculatorEngine(persisted, () => SolvedAt);
        Assert.Equal(CalculatorEngine.MaxHistory, engine.History.Count);

        Type(engine, "3+3=");
        Assert.Equal(CalculatorEngine.MaxHistory, engine.History.Count);
        Assert.Equal("6", engine.History[0].Result);
    }

    [Fact]
    public void RemovingAndClearingBumpTheHistoryVersion()
    {
        var engine = NewEngine();
        Type(engine, "1+1=2+2=");
        var version = engine.HistoryVersion;

        engine.Remove(engine.History[1]);
        Assert.Single(engine.History);
        Assert.True(engine.HistoryVersion > version);

        version = engine.HistoryVersion;
        engine.ClearHistory();
        Assert.Empty(engine.History);
        Assert.True(engine.HistoryVersion > version);
    }

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(1234.5, "1234.5")]
    [InlineData(-42.0, "-42")]
    [InlineData(123456789000000.0, "123456789000000")]
    [InlineData(1e15, "1e15")]
    [InlineData(-2.5e20, "-2.5e20")]
    [InlineData(1e-9, "1e-9")]
    [InlineData(0.000000016, "0.00000002")]
    public void FormatKeepsDisplayableDigits(double value, string expected)
    {
        Assert.Equal(expected, CalculatorEngine.Format(value));
    }

    [Fact]
    public void ScientificAnswersReadBackForTheNextSum()
    {
        var engine = NewEngine();

        Type(engine, "100000000*100000000=");
        Assert.Equal("1e16", engine.Display);

        Type(engine, "/100000000=");
        Assert.Equal("100000000", engine.Display);
    }
}
