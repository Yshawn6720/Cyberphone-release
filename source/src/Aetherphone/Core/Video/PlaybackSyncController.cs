namespace Aetherphone.Core.Video;

internal readonly record struct SyncDecision(bool SpeedChanged, double Speed, bool Seek, double SeekTarget,
    bool Reopen = false)
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
    internal const double MaxSeekLeadSeconds = 10.0;
    internal const int HardSeeksBeforeReopen = 3;

    private double appliedSpeed = 1d;
    private double seekSettleRemaining;
    private double seekLead;
    private double seekElapsed;
    private double seekTarget;
    private bool measuringSeek;
    private int consecutiveHardSeeks;
    private double previousTarget = double.NaN;

    internal double AppliedSpeed => appliedSpeed;

    internal double SeekLead => seekLead;

    internal void Reset()
    {
        appliedSpeed = 1d;
        seekLead = 0d;
        EndRound();
    }

    internal bool Release()
    {
        EndRound();
        if (appliedSpeed == 1d)
        {
            return false;
        }

        appliedSpeed = 1d;
        return true;
    }

    private void EndRound()
    {
        seekSettleRemaining = 0d;
        seekElapsed = 0d;
        measuringSeek = false;
        consecutiveHardSeeks = 0;
        previousTarget = double.NaN;
    }

    internal SyncDecision Step(double localPosition, double targetPosition, double durationSeconds, bool localSeeking,
        float deltaSeconds)
    {
        if (Math.Abs(targetPosition - previousTarget - deltaSeconds) > HardSeekToleranceSeconds)
        {
            consecutiveHardSeeks = 0;
        }

        previousTarget = targetPosition;
        if (measuringSeek)
        {
            seekElapsed += deltaSeconds;
            if (!localSeeking && Math.Abs(localPosition - seekTarget) <= HardSeekToleranceSeconds)
            {
                seekLead = Math.Min(seekElapsed, MaxSeekLeadSeconds);
                measuringSeek = false;
            }
        }

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
            return HardSeek(targetPosition, durationSeconds);
        }

        consecutiveHardSeeks = 0;
        if (magnitude <= DeadZoneSeconds)
        {
            return ApplySpeed(1d);
        }

        var deviation = Math.Min(1d, (magnitude - DeadZoneSeconds) / (FullNudgeSeconds - DeadZoneSeconds))
            * MaxSpeedDeviation;
        var desired = drift > 0d ? 1d - deviation : 1d + deviation;
        return ApplySpeed(Quantize(desired));
    }

    private SyncDecision HardSeek(double targetPosition, double durationSeconds)
    {
        seekSettleRemaining = SeekSettleSeconds;
        var speedChanged = appliedSpeed != 1d;
        appliedSpeed = 1d;
        var aim = consecutiveHardSeeks > 0 ? targetPosition + seekLead : targetPosition;
        if (durationSeconds > 0d)
        {
            aim = Math.Min(aim, durationSeconds - EndGuardSeconds);
        }

        aim = Math.Max(0d, aim);
        seekTarget = aim;
        seekElapsed = 0d;
        measuringSeek = true;
        consecutiveHardSeeks++;
        if (consecutiveHardSeeks < HardSeeksBeforeReopen)
        {
            return new SyncDecision(speedChanged, 1d, true, aim);
        }

        consecutiveHardSeeks = 1;
        return new SyncDecision(speedChanged, 1d, false, aim, true);
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
