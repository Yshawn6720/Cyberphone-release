using Aetherphone.Core;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Health;

internal static class HealthText
{
    private const string AverageFormat = "0.#";

    public static (string Value, string Unit) Metric(HealthMetric metric, double value, HealthUnits units,
        bool average)
    {
        switch (metric)
        {
            case HealthMetric.Water:
                var drinks = average ? value.ToString(AverageFormat, Loc.Culture) : HealthFormat.Number((long)value);
                return (drinks, Loc.Plural(L.Health.UnitDrinks, (int)Math.Round(value)));
            case HealthMetric.Steps:
                var steps = (long)Math.Round(value);
                return (HealthFormat.Number(steps), Loc.Plural(L.Health.UnitSteps, (int)Math.Min(steps, int.MaxValue)));
            case HealthMetric.Distance:
            case HealthMetric.Swimming:
                return HealthFormat.DistanceParts(value, units);
            default:
                return (HealthFormat.Duration(value), string.Empty);
        }
    }

    public static string Joined(HealthMetric metric, double value, HealthUnits units, bool average)
    {
        var (number, unit) = Metric(metric, value, units, average);
        return unit.Length == 0 ? number : string.Concat(number, " ", unit);
    }

    public static string Name(HealthMetric metric) => Loc.T(metric switch
    {
        HealthMetric.Water => L.Health.Water,
        HealthMetric.Steps => L.Health.Steps,
        HealthMetric.Distance => L.Health.OnFootDistance,
        HealthMetric.Swimming => L.Health.Swimming,
        _ => L.Health.ActiveTime,
    });

    public static string DayTitle(DateTime date)
    {
        var today = DateTime.Today;
        if (date == today)
        {
            return Loc.T(L.Time.Today);
        }

        if (date == today.AddDays(-1))
        {
            return Loc.T(L.Time.Yesterday);
        }

        return date.ToString("ddd, MMM d", Loc.Culture);
    }

    public static string GoalProgress(HealthGoal goal, double current, HealthUnits units)
    {
        var scope = Loc.T(goal.Scope switch
        {
            HealthGoalScope.Weekly => L.Health.ScopeThisWeek,
            HealthGoalScope.Session => L.Health.ScopeThisSession,
            HealthGoalScope.AllTime => L.Health.ScopeAllTime,
            _ => L.Health.Today,
        });
        return Loc.T(L.Health.GoalProgressLine, scope, GoalAmount(goal.Type, current, units),
            GoalAmount(goal.Type, goal.Target, units));
    }

    public static string GoalAmount(HealthGoalType type, double value, HealthUnits units) => type switch
    {
        HealthGoalType.Steps or HealthGoalType.HydrationCount or HealthGoalType.Teleports =>
            HealthFormat.Number((long)Math.Floor(Math.Max(0d, value))),
        HealthGoalType.ActiveTime => HealthFormat.Duration(value),
        HealthGoalType.HydrationVolume => HealthFormat.Volume(value, units),
        HealthGoalType.Calories => Loc.T(L.Health.Kcal, Math.Max(0d, value).ToString("0", Loc.Culture)),
        _ => HealthFormat.Distance(value, units),
    };
}
