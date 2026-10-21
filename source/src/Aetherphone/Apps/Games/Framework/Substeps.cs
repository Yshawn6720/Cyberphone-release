namespace Aetherphone.Apps.Games.Framework;

internal readonly struct Substeps
{
    public const int DefaultCap = 16;

    public readonly int Count;
    public readonly float Step;

    public Substeps(float deltaSeconds, float maxStepSeconds, int cap = DefaultCap)
    {
        if (deltaSeconds <= 0f)
        {
            Count = 0;
            Step = 0f;
            return;
        }

        Count = Math.Clamp((int)MathF.Ceiling(deltaSeconds / maxStepSeconds), 1, cap);
        Step = deltaSeconds / Count;
    }
}
