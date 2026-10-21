namespace Aetherphone.Apps.Games.Framework;

internal struct FixedStepClock
{
    private readonly float stepSeconds;
    private readonly float maxCatchUpSeconds;
    private float accumulator;

    public FixedStepClock(float stepSeconds, float maxCatchUpSeconds)
    {
        this.stepSeconds = stepSeconds;
        this.maxCatchUpSeconds = maxCatchUpSeconds;
        accumulator = 0f;
    }

    public readonly float Step => stepSeconds;

    public readonly float Alpha => stepSeconds <= 0f ? 0f : accumulator / stepSeconds;

    public int Advance(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return 0;
        }

        accumulator += MathF.Min(deltaSeconds, maxCatchUpSeconds);
        var count = 0;
        while (accumulator >= stepSeconds)
        {
            accumulator -= stepSeconds;
            count++;
        }

        return count;
    }

    public void Reset()
    {
        accumulator = 0f;
    }
}
