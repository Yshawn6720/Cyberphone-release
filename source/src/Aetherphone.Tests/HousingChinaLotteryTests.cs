using Aetherphone.Core.Housing;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HousingChinaLotteryTests
{
    private static readonly DateTime CycleStartUtc = new(2026, 8, 23, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EntryEndsUtc = CycleStartUtc.AddDays(5);
    private static readonly DateTime ResultsEndsUtc = CycleStartUtc.AddDays(9);
    private static readonly DateTime FirstSeenLongBeforeUtc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ObservedEntryEndUtc = new(2024, 11, 18, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ObservedResultsEndUtc = new(2026, 4, 1, 15, 0, 0, DateTimeKind.Utc);

    public static TheoryData<double> EntryHours() => new() { 0d, 1d, 60d, 119.9d };

    public static TheoryData<double> ResultsHours() => new() { 120d, 121d, 200d, 215.9d };

    [Theory]
    [MemberData(nameof(EntryHours))]
    public void OpenPlotIsInEntryForTheFirstFiveDaysOfTheCycle(double hoursIntoCycle)
    {
        var plot = OpenPlot(FirstSeenLongBeforeUtc);

        var inferred = HousingRestProvider.InferChinaLotteryPhase(plot, CycleStartUtc.AddHours(hoursIntoCycle));

        Assert.Equal((HousingLotteryPhase.Entry, EntryEndsUtc), inferred);
    }

    [Theory]
    [MemberData(nameof(ResultsHours))]
    public void OpenPlotIsInResultsForTheLastFourDaysOfTheCycle(double hoursIntoCycle)
    {
        var plot = OpenPlot(FirstSeenLongBeforeUtc);

        var inferred = HousingRestProvider.InferChinaLotteryPhase(plot, CycleStartUtc.AddHours(hoursIntoCycle));

        Assert.Equal((HousingLotteryPhase.Results, ResultsEndsUtc), inferred);
    }

    [Theory]
    [MemberData(nameof(EntryHours))]
    public void LotteryRowsAndOpenPlotsAgreeOnTheSameCycle(double hoursIntoCycle)
    {
        var nowUtc = CycleStartUtc.AddHours(hoursIntoCycle);
        var open = OpenPlot(FirstSeenLongBeforeUtc);
        var entryRow = LotteryRow(1, ObservedEntryEndUtc);
        var resultsRow = LotteryRow(2, ObservedResultsEndUtc);

        var fromOpen = HousingRestProvider.InferChinaLotteryPhase(open, nowUtc);

        Assert.Equal(fromOpen, HousingRestProvider.InferChinaLotteryPhase(entryRow, nowUtc));
        Assert.Equal(fromOpen, HousingRestProvider.InferChinaLotteryPhase(resultsRow, nowUtc));
    }

    [Fact]
    public void LotteryRowKeepsItsOwnDeadlineWhileItIsStillRunning()
    {
        var plot = LotteryRow(1, ObservedEntryEndUtc);

        var inferred = HousingRestProvider.InferChinaLotteryPhase(plot, ObservedEntryEndUtc.AddHours(-1));

        Assert.Equal((HousingLotteryPhase.Entry, ObservedEntryEndUtc), inferred);
    }

    [Fact]
    public void PlotIsUnavailableUntilItsFirstCycleStart()
    {
        var firstSeenUtc = CycleStartUtc.AddDays(-3);
        var plot = OpenPlot(firstSeenUtc);

        var inferred = HousingRestProvider.InferChinaLotteryPhase(plot, firstSeenUtc.AddHours(1));

        Assert.Equal((HousingLotteryPhase.Unavailable, CycleStartUtc), inferred);
    }

    [Fact]
    public void PlotWithoutTimestampsHasNoPhase()
    {
        var inferred = HousingRestProvider.InferChinaLotteryPhase(new ChinaSalesPlot(), CycleStartUtc);

        Assert.Equal((HousingLotteryPhase.Unknown, (DateTime?)null), inferred);
    }

    private static ChinaSalesPlot OpenPlot(DateTime firstSeenUtc) =>
        new()
        {
            FirstSeen = UnixSeconds(firstSeenUtc),
            LastSeen = UnixSeconds(firstSeenUtc.AddDays(1)),
            PurchaseType = 2,
        };

    private static ChinaSalesPlot LotteryRow(int state, DateTime endUtc) =>
        new()
        {
            State = state,
            EndTime = UnixSeconds(endUtc),
            UpdateTime = UnixSeconds(endUtc.AddDays(-2)),
            FirstSeen = UnixSeconds(endUtc.AddDays(-30)),
            LastSeen = UnixSeconds(endUtc.AddDays(-1)),
            PurchaseType = 2,
        };

    private static long UnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();
}
