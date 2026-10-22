namespace Aetherphone.Core.Telephony;

internal sealed class SendBudget
{
    private readonly int capacity;
    private readonly long refillMilliseconds;
    private int tokens;
    private long refilledAtTicks;

    public SendBudget(int capacity, long refillMilliseconds)
    {
        this.capacity = capacity;
        this.refillMilliseconds = refillMilliseconds;
        tokens = capacity;
    }

    public bool TryTake(long nowTicks)
    {
        var refilled = (nowTicks - refilledAtTicks) / refillMilliseconds;
        if (tokens + refilled >= capacity)
        {
            tokens = capacity;
            refilledAtTicks = nowTicks;
        }
        else
        {
            tokens += (int)refilled;
            refilledAtTicks += refilled * refillMilliseconds;
        }

        if (tokens == 0)
        {
            return false;
        }

        tokens--;
        return true;
    }
}
