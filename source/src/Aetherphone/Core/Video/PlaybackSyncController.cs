namespace Aetherphone.Core.Video;

internal readonly record struct SyncDecision(bool SpeedChanged, double Speed, bool Seek, double SeekTarget)
{
    internal static readonly SyncDecision Hold = new(false, 1d, false, 0d);
}

internal sealed class PlaybackSyncController
{
    internal const double DeadZoneSeconds = 0.3;
    internal const double FullNudgeSeconds = 1.5;
    internal const double MaxSpeedDeviation = 0.05;
    internal const double HardSeekToleranceSeconds = 3.0;
    internal const double SeekSettleSeconds = 2.0;
    internal const double EndGuardSeconds = 0.5;
    internal const double SpeedStep = 0.005;

    private double appliedSpeed = 1d;
    private double seekSettleRemaining;

    internal double AppliedSpeed => appliedSpeed;

    internal void Reset()
    {
        appliedSpeed = 1d;
        seekSettleRemaining = 0d;
    }

    internal bool Release()
    {
        seekSettleRemaining = 0d;
        if (appliedSpeed == 1d)
        {
            return false;
        }

        appliedSpeed = 1d;
        return true;
    }

    internal SyncDecision Step(double localPosition, double targetPosition, double durationSeconds, bool localSeeking,
        float deltaSeconds)
    {
        seekSettleRemaining = Math.Max(0d, seekSettleRemaining - deltaSeconds);
        if (localSeeking || seekSettleRemaining > 0d)
        {
            return SyncDecision.Hold;
        }

        var nearEnd = durationSeconds > 0d && targetPosition >= durationSeconds - EndGuardSeconds;
        if (nearEnd)
        {
            return ApplySpeed(1d);
        }

        var drift = localPosition - targetPosition;
        var magnitude = Math.Abs(drift);
        if (magnitude > HardSeekToleranceSeconds)
        {
            seekSettleRemaining = SeekSettleSeconds;
            var speedChanged = appliedSpeed != 1d;
            appliedSpeed = 1d;
            return new SyncDecision(speedChanged, 1d, true, Math.Max(0d, targetPosition));
        }

        if (magnitude <= DeadZoneSeconds)
        {
            return ApplySpeed(1d);
        }

        var deviation = Math.Min(1d, (magnitude - DeadZoneSeconds) / (FullNudgeSeconds - DeadZoneSeconds))
            * MaxSpeedDeviation;
        var desired = drift > 0d ? 1d - deviation : 1d + deviation;
        return ApplySpeed(Quantize(desired));
    }

    private SyncDecision ApplySpeed(double speed)
    {
        if (Math.Abs(speed - appliedSpeed) < SpeedStep / 2d)
        {
            return SyncDecision.Hold;
        }

        appliedSpeed = speed;
        return new SyncDecision(true, speed, false, 0d);
    }

    private static double Quantize(double speed) => Math.Round(speed / SpeedStep) * SpeedStep;
}
