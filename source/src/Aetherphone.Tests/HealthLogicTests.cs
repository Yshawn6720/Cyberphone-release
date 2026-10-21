using Aetherphone.Core.Health;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HealthLogicTests
{
    private const double Stride = 0.75;
    private static readonly DateTime Today = new(2026, 10, 3);

    private static HealthDay Day(int daysAgo, double walk = 0d, int drinks = 0, double active = 0d) =>
        new()
        {
            Date = HealthSeries.DateKey(Today.AddDays(-daysAgo)),
            WalkYalms = walk,
            ActiveSeconds = active,
            Drinks = Drinks(drinks),
        };

    private static List<HydrationEntry> Drinks(int count)
    {
        var drinks = new List<HydrationEntry>();
        for (var index = 0; index < count; index++)
        {
            drinks.Add(new HydrationEntry { KindKey = DrinkKeys.Water, Millilitres = 250d, Unix = 1 + index });
        }

        return drinks;
    }

    [Fact]
    public void FillLeavesGapsForDaysWithoutRecords()
    {
        var days = new List<HealthDay> { Day(6, drinks: 2), Day(3, drinks: 5), Day(0, drinks: 1) };
        Span<double> values = stackalloc double[7];
        Span<bool> recorded = stackalloc bool[7];
        var filled = HealthSeries.Fill(days, HealthMetric.Water, Stride, Today, values, recorded);
        Assert.Equal(3, filled);
        Assert.True(recorded[0]);
        Assert.False(recorded[1]);
        Assert.Equal(5d, values[3]);
        Assert.Equal(1d, values[6]);
        Assert.Equal(0d, values[5]);
    }

    [Fact]
    public void StepsComeFromOnFootDistanceAndStride()
    {
        var day = Day(0, walk: 750d);
        Assert.Equal(1000d, HealthSeries.DayValue(day, HealthMetric.Steps, Stride));
        Assert.Equal(750d, HealthSeries.DayValue(day, HealthMetric.Distance, Stride));
    }

    [Fact]
    public void AverageSkipsDaysWithoutRecords()
    {
        double[] values = { 4d, 0d, 8d };
        bool[] recorded = { true, false, true };
        Assert.Equal(6d, HealthSeries.Average(values, recorded));
        Assert.Equal(2, HealthSeries.BestIndex(values, recorded));
    }

    [Fact]
    public void TrendNeedsEnoughDaysInBothWeeks()
    {
        var days = new List<HealthDay> { Day(10, drinks: 4), Day(9, drinks: 4), Day(2, drinks: 8), Day(1, drinks: 8) };
        Assert.False(HealthSeries.Trend(days, HealthMetric.Water, Stride, Today).Available);
    }

    [Fact]
    public void TrendReportsDirectionOfWeeklyAverages()
    {
        var days = new List<HealthDay>
        {
            Day(12, drinks: 4), Day(11, drinks: 4), Day(10, drinks: 4),
            Day(3, drinks: 8), Day(2, drinks: 8), Day(1, drinks: 8),
        };
        var trend = HealthSeries.Trend(days, HealthMetric.Water, Stride, Today);
        Assert.True(trend.Available);
        Assert.Equal(TrendDirection.Up, trend.Direction);
        Assert.Equal(8d, trend.Recent);
        Assert.Equal(4d, trend.Previous);
    }

    [Fact]
    public void SmallChangesReadAsSteady()
    {
        Assert.Equal(TrendDirection.Steady, HealthSeries.Direction(102d, 100d));
        Assert.Equal(TrendDirection.Down, HealthSeries.Direction(80d, 100d));
    }

    [Fact]
    public void WeightInsertKeepsEntriesSortedAndCapped()
    {
        var entries = new List<WeightEntry>
        {
            new() { Unix = 100, Kilograms = 70d },
            new() { Unix = 300, Kilograms = 71d },
        };
        WeightHistory.Insert(entries, new WeightEntry { Unix = 200, Kilograms = 70.5d });
        Assert.Equal(new long[] { 100, 200, 300 }, entries.ConvertAll(entry => entry.Unix));
    }

    [Fact]
    public void WeightBucketsAverageEntriesPerBucket()
    {
        var entries = new List<WeightEntry>
        {
            new() { Unix = 1_000, Kilograms = 70d },
            new() { Unix = 1_050, Kilograms = 72d },
            new() { Unix = 1_250, Kilograms = 74d },
        };
        Span<float> values = stackalloc float[3];
        Span<bool> present = stackalloc bool[3];
        var filled = WeightHistory.Bucket(entries, 1_000, 100, values, present);
        Assert.Equal(2, filled);
        Assert.Equal(71f, values[0]);
        Assert.False(present[1]);
        Assert.Equal(74f, values[2]);
    }

    [Fact]
    public void WeightChangeNeedsTwoEntriesInRange()
    {
        var entries = new List<WeightEntry>
        {
            new() { Unix = 100, Kilograms = 70d },
            new() { Unix = 500, Kilograms = 69.4d },
            new() { Unix = 900, Kilograms = 68.9d },
        };
        Assert.Equal(-0.5d, WeightHistory.ChangeSince(entries, 400)!.Value, 6);
        Assert.Null(WeightHistory.ChangeSince(entries, 800));
    }

    [Fact]
    public void NormalizeDropsInvalidEntries()
    {
        var entries = new List<WeightEntry>
        {
            new() { Unix = 300, Kilograms = 70d },
            new() { Unix = 0, Kilograms = 70d },
            new() { Unix = 200, Kilograms = double.NaN },
            new() { Unix = 100, Kilograms = 900d },
            new() { Unix = 150, Kilograms = 65d },
        };
        WeightHistory.Normalize(entries);
        Assert.Equal(new long[] { 150, 300 }, entries.ConvertAll(entry => entry.Unix));
    }

    [Fact]
    public void LegacyWeightIsAdoptedOnlyIntoAnEmptyLog()
    {
        var entries = new List<WeightEntry>();
        WeightHistory.Adopt(entries, 48d, 500);
        Assert.Single(entries);
        WeightHistory.Adopt(entries, 60d, 900);
        Assert.Single(entries);
        Assert.Equal(48d, WeightHistory.Latest(entries));
        var empty = new List<WeightEntry>();
        WeightHistory.Adopt(empty, null, 500);
        Assert.Empty(empty);
    }

    [Fact]
    public void ServingSnapsToUnitSteps()
    {
        Assert.Equal(300d, HealthFormat.SnapServing(310d, HealthUnits.Metric));
        Assert.Equal(HealthFormat.MinServingMillilitres, HealthFormat.SnapServing(5d, HealthUnits.Metric));
        Assert.Equal(8d, Math.Round(HealthFormat.SnapServing(240d, HealthUnits.Imperial) / 29.5735d), 6);
    }

    [Fact]
    public void LegacyProfilesLoadUnchanged()
    {
        var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "aetherphone-health-" + Guid.NewGuid()));
        try
        {
            var store = new HealthStore(directory);
            const ulong contentId = 0x1234UL;
            const string legacy = "{\"version\":1,\"setup\":true,\"units\":1,\"weightKg\":62.5,\"goalSteps\":8000," +
                                  "\"goalDrinks\":6,\"days\":[{\"date\":\"2026-10-02\",\"walk\":1500.0,\"drinks\":" +
                                  "[{\"t\":10,\"kk\":\"tea\",\"k\":\"\",\"ml\":250.0}]}],\"goalList\":[{\"id\":\"a\"," +
                                  "\"nk\":\"walk1000\",\"type\":0,\"scope\":0,\"target\":1000.0,\"enabled\":true}]," +
                                  "\"streak\":4}";
            File.WriteAllText(Path.Combine(directory.FullName, contentId.ToString("X16") + ".json"), legacy);
            var profile = store.Load(contentId);
            Assert.True(profile.SetupCompleted);
            Assert.Equal(HealthUnits.Metric, profile.Units);
            Assert.Equal(62.5d, profile.WeightKg);
            Assert.Equal(8000, profile.DailyStepGoal);
            Assert.Equal(6, profile.DailyHydrationGoal);
            Assert.Equal(4, profile.StreakDays);
            Assert.Single(profile.Days);
            Assert.Equal(DrinkKeys.Tea, profile.Days[0].Drinks[0].KindKey);
            Assert.Single(profile.Goals);
            Assert.Single(profile.WeightLog);
            Assert.Equal(62.5d, profile.WeightLog[0].Kilograms);
            Assert.Equal(HealthFormat.GlassMillilitres, profile.ServingMillilitres);
            Assert.Equal(DrinkKeys.Water, profile.ServingKind);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
