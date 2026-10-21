namespace Aetherphone.Core.Activity;

internal static class ActivityGoalSteps
{
    public const int GoalCount = 4;
    public const int EndgameGoal = 3;
    public const float LevelsStep = 0.5f;
    public const int LevelsStepCount = 10;
    public const int MinDuties = 1;
    public const int MaxDuties = 10;

    private static readonly long[] GilSteps = { 10000, 25000, 50000, 100000, 250000, 500000, 1000000 };
    private static readonly long[] EndgameSteps = { 50, 100, 150, 200, 300, 450, 600, 900, 1200 };

    public static int RingOf(int goal) => goal == EndgameGoal ? 0 : goal;

    public static int Count(int goal) => goal switch
    {
        0 => LevelsStepCount,
        1 => MaxDuties - MinDuties + 1,
        2 => GilSteps.Length,
        _ => EndgameSteps.Length,
    };

    public static int IndexOf(int goal, in ActivityTargets targets) => goal switch
    {
        0 => Math.Clamp((int)MathF.Round(targets.Levels / LevelsStep) - 1, 0, LevelsStepCount - 1),
        1 => Math.Clamp(targets.Duties - MinDuties, 0, MaxDuties - MinDuties),
        2 => Nearest(GilSteps, targets.Gil),
        _ => Nearest(EndgameSteps, targets.Endgame),
    };

    public static ActivityTargets With(int goal, int index, in ActivityTargets targets)
    {
        var clamped = Math.Clamp(index, 0, Count(goal) - 1);
        return goal switch
        {
            0 => targets with { Levels = (clamped + 1) * LevelsStep },
            1 => targets with { Duties = clamped + MinDuties },
            2 => targets with { Gil = GilSteps[clamped] },
            _ => targets with { Endgame = EndgameSteps[clamped] },
        };
    }

    public static void Apply(Configuration configuration, in ActivityTargets targets)
    {
        configuration.ActivityGoalLevels = targets.Levels;
        configuration.ActivityGoalDuties = targets.Duties;
        configuration.ActivityGoalGil = targets.Gil;
        configuration.ActivityGoalEndgame = targets.Endgame;
    }

    private static int Nearest(long[] steps, long value)
    {
        var best = 0;
        for (var index = 1; index < steps.Length; index++)
        {
            if (Math.Abs(steps[index] - value) < Math.Abs(steps[best] - value))
            {
                best = index;
            }
        }

        return best;
    }
}
