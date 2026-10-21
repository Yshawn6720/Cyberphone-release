namespace Aetherphone.Core.Animation;

// Unused today: Velvet dropped its card deck, and this is the throw kept for the next surface that wants one.
internal struct SwipeDeck
{
    public const float CommitFraction = 0.32f;

    private const float ExitOverhang = 48f;
    private const float ExitSmoothTime = 0.16f;
    private const float SettleEpsilon = 2f;

    private Spring slide;
    private int exit;

    public readonly float Offset => slide.Value;

    public readonly int Direction => exit;

    public readonly bool Throwing => exit != 0;

    public bool Throw(int direction)
    {
        if (exit != 0 || direction == 0)
        {
            return false;
        }

        exit = Math.Sign(direction);
        return true;
    }

    public bool Step(float width, float scale, float deltaSeconds)
    {
        if (exit == 0)
        {
            return false;
        }

        var target = exit * (width + ExitOverhang * scale);
        slide.Step(target, ExitSmoothTime, deltaSeconds);
        return MathF.Abs(slide.Value - target) <= SettleEpsilon * scale;
    }

    public int Commit()
    {
        var direction = exit;
        exit = 0;
        slide.SnapTo(0f);
        return direction;
    }

    public void Reset()
    {
        exit = 0;
        slide.SnapTo(0f);
    }

    public readonly float StampProgress(float width) =>
        MathF.Min(1f, MathF.Abs(slide.Value) / MathF.Max(1f, width * CommitFraction));
}
