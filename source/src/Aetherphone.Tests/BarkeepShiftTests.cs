using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BarkeepShiftTests
{
    private const long StartedAt = 1_754_784_000;

    [Fact]
    public void TheFinishGateHoldsUntilSixtyOneSecondsOfServerAnchoredTime()
    {
        var shift = Shift();
        Assert.False(shift.CanFinish(0));
        Assert.False(shift.CanFinish(60));
        Assert.False(shift.CanFinish(60.9));
        Assert.True(shift.CanFinish(61));
        Assert.True(shift.CanFinish(120));
    }

    [Fact]
    public void AutoFinishFiresAtTwoNinetyWithMarginBeforeTheServerForfeit()
    {
        var shift = Shift();
        Assert.False(shift.ShouldAutoFinish(289.9));
        Assert.True(shift.ShouldAutoFinish(290));
        Assert.True(BarkeepShift.AutoFinishSeconds < BarkeepRules.MaxFinishSeconds);
        Assert.True(BarkeepShift.FinishEarliestSeconds > BarkeepRules.MinFinishSeconds);
    }

    [Fact]
    public void ElapsedTimeAnchorsToTheServerStartAndNeverRunsBackwards()
    {
        var shift = Shift();
        Assert.Equal(0, shift.ElapsedSeconds(StartedAt - 50));
        Assert.Equal(0, shift.ElapsedSeconds(StartedAt));
        Assert.Equal(72.5, shift.ElapsedSeconds(StartedAt + 72.5), 3);
    }

    [Fact]
    public void PatronsOnlyArriveAtTheirScriptedSecond()
    {
        var shift = Shift();
        Assert.False(shift.CurrentPatronArrived(4.9));
        Assert.True(shift.CurrentPatronArrived(5));
        Assert.Equal(3, shift.SecondsUntilCurrentPatron(2), 3);
        Assert.Equal(0, shift.SecondsUntilCurrentPatron(9), 3);
    }

    [Fact]
    public void CommittedGradesScoreWithTheServerFormulaOrderByOrder()
    {
        var shift = Shift();
        shift.CommitStepGrade(100);
        shift.CommitStepGrade(70);
        Assert.Equal(0, shift.CompletedOrders);
        Assert.Equal(0, shift.Score);
        shift.CommitStepGrade(40);
        Assert.Equal(1, shift.CompletedOrders);
        Assert.Equal(70, shift.Score);
        shift.CommitStepGrade(100);
        shift.CommitStepGrade(100);
        Assert.Equal(2, shift.CompletedOrders);
        Assert.Equal(170, shift.Score);
        Assert.True(shift.AllServed);
    }

    [Fact]
    public void AnOutOfDomainGradeIsCommittedAsAMiss()
    {
        var shift = Shift();
        shift.CommitStepGrade(55);
        shift.CommitStepGrade(100);
        shift.CommitStepGrade(100);
        Assert.Equal((0 + 100 + 100) / 3, shift.Score);
    }

    [Fact]
    public void FinishOrdersAreAStrictPrefixOfCompletedOrders()
    {
        var shift = Shift();
        shift.CommitStepGrade(100);
        shift.CommitStepGrade(70);
        shift.CommitStepGrade(40);
        shift.CommitStepGrade(100);
        var orders = shift.BuildFinishOrders();
        var order = Assert.Single(orders);
        Assert.Equal(new[] { 100, 70, 40 }, order.StepGrades);
    }

    [Fact]
    public void TheFinishRequestSerializesTheShapeTheBackendValidates()
    {
        var shift = Shift();
        shift.CommitStepGrade(100);
        shift.CommitStepGrade(70);
        shift.CommitStepGrade(40);
        shift.CommitStepGrade(0);
        shift.CommitStepGrade(100);
        var request = new CasinoBarkeepFinishRequest("round9", shift.BuildFinishOrders());
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoBarkeepFinishRequest);
        Assert.Equal("{\"roundId\":\"round9\",\"orders\":[{\"stepGrades\":[100,70,40]},{\"stepGrades\":[0,100]}]}",
            json);
    }

    [Fact]
    public void FromStartAcceptsTheServerScriptAndKeepsItsAnchors()
    {
        var start = new CasinoBarkeepStartDto(true, string.Empty, "r5", new[]
        {
            new CasinoBarkeepPatronDto(2, new[] { 0, 1 }),
            new CasinoBarkeepPatronDto(31, new[] { 3, 2, 1 }),
        }, 200, StartedAt, StartedAt + 300, "next", 80);
        var shift = BarkeepShift.FromStart(start);
        Assert.NotNull(shift);
        Assert.Equal("r5", shift.RoundId);
        Assert.Equal(StartedAt, shift.StartedAtUnixSeconds);
        Assert.Equal(2, shift.PatronCount);
        Assert.Equal(200, shift.MaxScore);
        Assert.Equal(0, shift.CurrentStepKind);
    }

    [Fact]
    public void FromStartRejectsRefusalsAndMalformedScripts()
    {
        Assert.Null(BarkeepShift.FromStart(new CasinoBarkeepStartDto(false, "cooldown", "r5")));
        Assert.Null(BarkeepShift.FromStart(new CasinoBarkeepStartDto(true, "", "r5",
            Array.Empty<CasinoBarkeepPatronDto>(), 0, StartedAt, StartedAt + 300, "next", 0)));
        Assert.Null(BarkeepShift.FromStart(new CasinoBarkeepStartDto(true, "", "r5",
            new[] { new CasinoBarkeepPatronDto(2, new[] { 0, 4 }) }, 100, StartedAt, StartedAt + 300, "next", 0)));
        Assert.Null(BarkeepShift.FromStart(new CasinoBarkeepStartDto(true, "", "r5",
            new[] { new CasinoBarkeepPatronDto(2, new[] { 0, 1 }) }, 100, 0, 300, "next", 0)));
        Assert.Null(BarkeepShift.FromStart(new CasinoBarkeepStartDto(true, "", "",
            new[] { new CasinoBarkeepPatronDto(2, new[] { 0, 1 }) }, 100, StartedAt, StartedAt + 300, "next", 0)));
    }

    [Fact]
    public void ThePracticeScriptStaysInsideTheServerDomain()
    {
        var random = new Random(42);
        for (var run = 0; run < 20; run++)
        {
            var patrons = BarkeepShift.PracticeScript(random);
            Assert.Contains(patrons.Length, BarkeepRules.PatronBuckets);
            for (var patronIndex = 0; patronIndex < patrons.Length; patronIndex++)
            {
                Assert.InRange(patrons[patronIndex].ArrivalSecond, 0, BarkeepRules.ShiftSeconds - 1);
                Assert.InRange(patrons[patronIndex].StepKinds.Length, 2, 4);
                for (var stepIndex = 0; stepIndex < patrons[patronIndex].StepKinds.Length; stepIndex++)
                {
                    Assert.InRange(patrons[patronIndex].StepKinds[stepIndex], 0,
                        BarkeepRules.StepKindCount - 1);
                }
            }
        }
    }

    [Fact]
    public void LastCallWarnsBeforeTheForfeitAndCountsDownToIt()
    {
        var shift = Shift();
        Assert.False(shift.InLastCall(239));
        Assert.True(shift.InLastCall(240));
        Assert.Equal(60, shift.SecondsUntilForfeit(240), 3);
        Assert.Equal(0, shift.SecondsUntilForfeit(301), 3);
    }

    private static BarkeepShift Shift()
    {
        var patrons = new[]
        {
            new BarkeepPatronScript(5, new[] { 0, 1, 2 }),
            new BarkeepPatronScript(30, new[] { 3, 0 }),
        };
        return new BarkeepShift("round9", patrons, StartedAt);
    }
}
