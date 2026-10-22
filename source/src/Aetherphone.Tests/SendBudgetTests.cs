using Aetherphone.Core.Telephony;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SendBudgetTests
{
    private const int Burst = 4;
    private const long RefillMilliseconds = 1000;
    private const long StartTicks = 50_000;

    [Fact]
    public void SpendsTheBurstThenRefusesUntilOneTokenRefills()
    {
        var budget = new SendBudget(Burst, RefillMilliseconds);
        for (var index = 0; index < Burst; index++)
        {
            Assert.True(budget.TryTake(StartTicks));
        }

        Assert.False(budget.TryTake(StartTicks));
        Assert.False(budget.TryTake(StartTicks + RefillMilliseconds - 1));
        Assert.True(budget.TryTake(StartTicks + RefillMilliseconds));
        Assert.False(budget.TryTake(StartTicks + RefillMilliseconds));
    }

    [Fact]
    public void ALongPauseRefillsNoMoreThanTheBurst()
    {
        var budget = new SendBudget(Burst, RefillMilliseconds);
        for (var index = 0; index < Burst; index++)
        {
            Assert.True(budget.TryTake(StartTicks));
        }

        var later = StartTicks + RefillMilliseconds * 60;
        var accepted = 0;
        for (var index = 0; index < Burst * 3; index++)
        {
            if (budget.TryTake(later))
            {
                accepted++;
            }
        }

        Assert.Equal(Burst, accepted);
    }
}
