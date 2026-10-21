namespace Aetherphone.Core.Calculator;

[Serializable]
internal sealed class CalculatorHistoryRecord
{
    public string Expression { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public long SolvedAtUnix { get; set; }
}
