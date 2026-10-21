using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScreenDrawDiagnosticsTests
{
    [Fact]
    public void RepeatedOutcomeLogsOnlyWhenItChanges()
    {
        var diagnostics = new ScreenDrawDiagnostics();

        Assert.True(diagnostics.ShouldLog(ScreenDrawOutcome.NoSceneDepth));
        for (var frame = 0; frame < 1000; frame++)
        {
            Assert.False(diagnostics.ShouldLog(ScreenDrawOutcome.NoSceneDepth));
        }

        Assert.True(diagnostics.ShouldLog(ScreenDrawOutcome.Drawn));
        Assert.False(diagnostics.ShouldLog(ScreenDrawOutcome.Drawn));
    }

    [Fact]
    public void FlickeringOutcomesStopLoggingAtTheLimit()
    {
        var diagnostics = new ScreenDrawDiagnostics();
        var logged = 0;

        for (var frame = 0; frame < 10000; frame++)
        {
            var outcome = frame % 2 == 0 ? ScreenDrawOutcome.Drawn : ScreenDrawOutcome.NoCamera;
            if (diagnostics.ShouldLog(outcome))
            {
                logged++;
            }
        }

        Assert.Equal(ScreenDrawDiagnostics.MaxLoggedChanges, logged);
        Assert.True(diagnostics.ReachedLimit);
    }
}
