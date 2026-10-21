namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisLevelSystem
{
    private const int LinesPerLevel = 10;
    private const float ClassicBaseDropInterval = 0.72f;
    private const float ClassicDropIntervalStep = 0.055f;
    private const float ModernBaseDropInterval = 0.75f;
    private const float ModernDropIntervalStep = 0.028f;
    private const float MinimumDropInterval = 0.08f;
    private TetrisRuleset ruleset;
    public int Level { get; private set; } = 1;
    public int TotalLinesCleared { get; private set; }

    public float DropInterval => ruleset == TetrisRuleset.Modern
        ? MathF.Max(MinimumDropInterval, ModernBaseDropInterval - ModernDropIntervalStep * (Level - 1))
        : MathF.Max(MinimumDropInterval, ClassicBaseDropInterval - ClassicDropIntervalStep * (Level - 1));

    public void Reset() => Reset(TetrisRuleset.Classic);

    public void Reset(TetrisRuleset nextRuleset)
    {
        ruleset = nextRuleset;
        Level = 1;
        TotalLinesCleared = 0;
    }

    public void RegisterClearedLines(int clearedLines)
    {
        if (clearedLines <= 0)
        {
            return;
        }

        TotalLinesCleared += clearedLines;
        var nextLevel = 1 + TotalLinesCleared / LinesPerLevel;
        if (nextLevel > Level)
        {
            Level = nextLevel;
        }
    }
}
