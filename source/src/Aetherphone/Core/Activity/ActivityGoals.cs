namespace Aetherphone.Core.Activity;

internal readonly record struct ActivityTargets(float Levels, int Duties, long Gil, long Endgame)
{
    public static ActivityTargets From(Configuration configuration) =>
        new(configuration.ActivityGoalLevels, configuration.ActivityGoalDuties, configuration.ActivityGoalGil,
            configuration.ActivityGoalEndgame);
}

internal static class ActivityGoals
{
    public const float ClosedThreshold = 0.999f;
    public const int RingCount = 3;

    public static float ProgressFraction(Configuration configuration, ActivityDay day) =>
        ProgressFraction(ActivityTargets.From(configuration), day);

    public static float AdventureFraction(Configuration configuration, ActivityDay day) =>
        AdventureFraction(ActivityTargets.From(configuration), day);

    public static float FortuneFraction(Configuration configuration, ActivityDay day) =>
        FortuneFraction(ActivityTargets.From(configuration), day);

    public static bool AllClosed(Configuration configuration, ActivityDay day) =>
        AllClosed(ActivityTargets.From(configuration), day);

    public static bool ShowsEndgame(Configuration configuration, ActivityDay day) =>
        ShowsEndgame(ActivityTargets.From(configuration), day);

    public static float LevelFraction(in ActivityTargets targets, ActivityDay day) =>
        targets.Levels <= 0f ? 0f : day.LevelUnitsGained / targets.Levels;

    public static float EndgameFraction(in ActivityTargets targets, ActivityDay day) =>
        targets.Endgame <= 0 ? 0f : day.EndgameEarned / (float)targets.Endgame;

    public static float ProgressFraction(in ActivityTargets targets, ActivityDay day) =>
        LevelFraction(targets, day) + EndgameFraction(targets, day);

    public static bool ShowsEndgame(in ActivityTargets targets, ActivityDay day)
    {
        var levels = LevelFraction(targets, day);
        var endgame = EndgameFraction(targets, day);
        return endgame > levels || (endgame == levels && day.Capped);
    }

    public static float AdventureFraction(in ActivityTargets targets, ActivityDay day) =>
        targets.Duties <= 0 ? 0f : day.DutiesCompleted / (float)targets.Duties;

    public static float FortuneFraction(in ActivityTargets targets, ActivityDay day) =>
        targets.Gil <= 0 ? 0f : day.GilEarned / (float)targets.Gil;

    public static float Fraction(in ActivityTargets targets, ActivityDay day, int ring) => ring switch
    {
        0 => ProgressFraction(targets, day),
        1 => AdventureFraction(targets, day),
        _ => FortuneFraction(targets, day),
    };

    public static bool AllClosed(in ActivityTargets targets, ActivityDay day) =>
        ProgressFraction(targets, day) >= ClosedThreshold &&
        AdventureFraction(targets, day) >= ClosedThreshold &&
        FortuneFraction(targets, day) >= ClosedThreshold;
}
