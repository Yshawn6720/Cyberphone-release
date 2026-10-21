using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BarkeepGradingTests
{
    [Fact]
    public void QuantizeOnlyEverEmitsTheWireDomain()
    {
        for (var errorStep = -200; errorStep <= 200; errorStep++)
        {
            var grade = BarkeepGrading.Quantize(errorStep * 0.01f);
            Assert.True(BarkeepRules.IsValidGrade(grade));
        }
    }

    [Fact]
    public void EveryPourGradeStaysInTheDomain()
    {
        for (var fillStep = 0; fillStep <= 100; fillStep++)
        {
            var grade = BarkeepGrading.GradePour(fillStep * 0.01f, 0.7f);
            Assert.True(BarkeepRules.IsValidGrade(grade));
        }
    }

    [Fact]
    public void EveryShakeGradeStaysInTheDomain()
    {
        for (var errorStep = 0; errorStep <= 100; errorStep++)
        {
            var grade = BarkeepGrading.GradeShake(errorStep * 0.01f);
            Assert.True(BarkeepRules.IsValidGrade(grade));
        }
    }

    [Fact]
    public void EveryLayerGradeStaysInTheDomain()
    {
        for (var offsetStep = 0; offsetStep <= 100; offsetStep++)
        {
            var grade = BarkeepGrading.GradeLayer(offsetStep * 0.01f);
            Assert.True(BarkeepRules.IsValidGrade(grade));
        }
    }

    [Fact]
    public void EveryGarnishGradeStaysInTheDomain()
    {
        for (var offsetStep = 0; offsetStep <= 100; offsetStep++)
        {
            var grade = BarkeepGrading.GradeGarnish(offsetStep * 0.01f);
            Assert.True(BarkeepRules.IsValidGrade(grade));
        }
    }

    [Fact]
    public void PerfectAtZeroErrorAndMissWhenFarOff()
    {
        Assert.Equal(BarkeepGrading.PerfectGrade, BarkeepGrading.GradePour(0.7f, 0.7f));
        Assert.Equal(BarkeepGrading.PerfectGrade, BarkeepGrading.GradeShake(0f));
        Assert.Equal(BarkeepGrading.PerfectGrade, BarkeepGrading.GradeLayer(0f));
        Assert.Equal(BarkeepGrading.PerfectGrade, BarkeepGrading.GradeGarnish(0f));
        Assert.Equal(BarkeepGrading.MissGrade, BarkeepGrading.GradePour(0f, 0.9f));
        Assert.Equal(BarkeepGrading.MissGrade, BarkeepGrading.GradeShake(1f));
        Assert.Equal(BarkeepGrading.MissGrade, BarkeepGrading.GradeLayer(1f));
        Assert.Equal(BarkeepGrading.MissGrade, BarkeepGrading.GradeGarnish(1f));
    }

    [Fact]
    public void GradesNeverImproveAsTheErrorGrows()
    {
        var previous = int.MaxValue;
        for (var errorStep = 0; errorStep <= 150; errorStep++)
        {
            var grade = BarkeepGrading.Quantize(errorStep * 0.01f);
            Assert.True(grade <= previous);
            previous = grade;
        }
    }

    [Fact]
    public void NanAndNegativeInputsStillLandInTheDomain()
    {
        Assert.Equal(BarkeepGrading.MissGrade, BarkeepGrading.Quantize(float.NaN));
        Assert.Equal(BarkeepGrading.PerfectGrade, BarkeepGrading.Quantize(-0.1f));
        Assert.True(BarkeepRules.IsValidGrade(BarkeepGrading.GradePour(-0.5f, 0.7f)));
    }
}
