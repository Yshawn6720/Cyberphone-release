using Aetherphone.Core.Health;

namespace Aetherphone.Apps.Health;

internal enum HealthViewKind : byte
{
    Summary,
    Water,
    Metric,
    Weight,
    Goals,
    Settings,
}

internal readonly record struct HealthView(HealthViewKind Kind, HealthMetric Metric)
{
    public static HealthView Summary() => new(HealthViewKind.Summary, HealthMetric.Steps);

    public static HealthView Water() => new(HealthViewKind.Water, HealthMetric.Water);

    public static HealthView Of(HealthMetric metric) =>
        metric == HealthMetric.Water ? Water() : new HealthView(HealthViewKind.Metric, metric);

    public static HealthView Weight() => new(HealthViewKind.Weight, HealthMetric.Steps);

    public static HealthView Goals() => new(HealthViewKind.Goals, HealthMetric.Steps);

    public static HealthView Settings() => new(HealthViewKind.Settings, HealthMetric.Steps);
}
