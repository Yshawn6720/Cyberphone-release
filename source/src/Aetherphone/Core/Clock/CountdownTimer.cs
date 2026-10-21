namespace Aetherphone.Core.Clock;

internal enum CountdownPhase : byte
{
    Idle,
    Running,
    Paused,
    Finished,
}

internal static class CountdownTimer
{
    public const int RecentCapacity = 6;
    public const int MaxSeconds = 24 * 3600 - 1;

    public static CountdownPhase Phase(Configuration configuration, DateTime utcNow, out double remainingSeconds)
    {
        remainingSeconds = 0d;
        if (configuration.TimerPausedSeconds > 0)
        {
            remainingSeconds = configuration.TimerPausedSeconds;
            return CountdownPhase.Paused;
        }

        if (configuration.TimerEndsAtUtc is not { } endsAt)
        {
            return CountdownPhase.Idle;
        }

        remainingSeconds = (endsAt - utcNow).TotalSeconds;
        return remainingSeconds > 0d ? CountdownPhase.Running : CountdownPhase.Finished;
    }

    public static float Fraction(Configuration configuration, double remainingSeconds) =>
        Math.Clamp((float)(remainingSeconds / Math.Max(1, configuration.TimerDurationSeconds)), 0f, 1f);

    public static void Start(Configuration configuration, int seconds, string label, DateTime utcNow)
    {
        if (seconds <= 0)
        {
            return;
        }

        var clamped = Math.Min(seconds, MaxSeconds);
        configuration.TimerDurationSeconds = clamped;
        configuration.TimerEndsAtUtc = utcNow.AddSeconds(clamped);
        configuration.TimerPausedSeconds = 0;
        configuration.TimerNotified = false;
        configuration.TimerLabel = label;
        Remember(configuration.ClockRecentTimers, clamped);
    }

    public static void Pause(Configuration configuration, DateTime utcNow)
    {
        if (Phase(configuration, utcNow, out var remaining) != CountdownPhase.Running)
        {
            return;
        }

        configuration.TimerPausedSeconds = Math.Max(1, (int)Math.Ceiling(remaining));
        configuration.TimerEndsAtUtc = null;
    }

    public static void Resume(Configuration configuration, DateTime utcNow)
    {
        if (configuration.TimerPausedSeconds <= 0)
        {
            return;
        }

        configuration.TimerEndsAtUtc = utcNow.AddSeconds(configuration.TimerPausedSeconds);
        configuration.TimerPausedSeconds = 0;
        configuration.TimerNotified = false;
    }

    public static void Cancel(Configuration configuration)
    {
        configuration.TimerEndsAtUtc = null;
        configuration.TimerPausedSeconds = 0;
        configuration.TimerNotified = false;
    }

    public static void Remember(List<int> recents, int seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        recents.Remove(seconds);
        recents.Insert(0, seconds);
        if (recents.Count > RecentCapacity)
        {
            recents.RemoveRange(RecentCapacity, recents.Count - RecentCapacity);
        }
    }
}
