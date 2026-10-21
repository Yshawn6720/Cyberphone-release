namespace Aetherphone.Core.Casino;

internal enum CasinoSeatStage
{
    Watching,
    Sitting,
    Seated,
    Elsewhere,
    Claiming,
    Standing,
}

internal enum CasinoSeatSettle
{
    None,
    Bound,
    Released,
}

internal enum CasinoSeatSignal
{
    SitRequested,
    SitGranted,
    SitRefused,
    SeatBoundHere,
    SeatBoundElsewhere,
    TakeOverRequested,
    ClaimGranted,
    ClaimRefused,
    StandRequested,
    StandGranted,
    StandQueued,
    StandRefused,
    SeatLost,
    Left,
}

internal static class CasinoSeatMachine
{
    public const long SettleGraceMilliseconds = 6_000;

    public static CasinoSeatStage Next(CasinoSeatStage held, CasinoSeatSignal signal)
    {
        if (signal == CasinoSeatSignal.Left)
        {
            return CasinoSeatStage.Watching;
        }

        if (signal == CasinoSeatSignal.SeatLost)
        {
            return held is CasinoSeatStage.Sitting or CasinoSeatStage.Claiming ? held : CasinoSeatStage.Watching;
        }

        return held switch
        {
            CasinoSeatStage.Watching => FromWatching(signal),
            CasinoSeatStage.Sitting => FromSitting(signal),
            CasinoSeatStage.Seated => FromSeated(signal),
            CasinoSeatStage.Elsewhere => FromElsewhere(signal),
            CasinoSeatStage.Claiming => FromClaiming(signal),
            _ => FromStanding(signal),
        };
    }

    public static bool Busy(CasinoSeatStage stage)
    {
        return stage is CasinoSeatStage.Sitting or CasinoSeatStage.Claiming or CasinoSeatStage.Standing;
    }

    public static bool Holds(CasinoSeatStage stage)
    {
        return stage is CasinoSeatStage.Seated or CasinoSeatStage.Standing;
    }

    public static bool Watching(CasinoSeatStage stage)
    {
        return stage is CasinoSeatStage.Watching or CasinoSeatStage.Sitting;
    }

    public static bool ShowsTakeOver(CasinoSeatStage stage)
    {
        return stage is CasinoSeatStage.Elsewhere or CasinoSeatStage.Claiming;
    }

    public static CasinoSeatSignal SignalFor(bool hasSeat, bool boundElsewhere)
    {
        if (!hasSeat)
        {
            return CasinoSeatSignal.SeatLost;
        }

        return boundElsewhere ? CasinoSeatSignal.SeatBoundElsewhere : CasinoSeatSignal.SeatBoundHere;
    }

    public static CasinoSeatSettle SettleFor(CasinoSeatStage requested, bool atHandEnd)
    {
        return requested switch
        {
            CasinoSeatStage.Sitting => CasinoSeatSettle.Bound,
            CasinoSeatStage.Claiming => CasinoSeatSettle.Bound,
            CasinoSeatStage.Standing => atHandEnd ? CasinoSeatSettle.None : CasinoSeatSettle.Released,
            _ => CasinoSeatSettle.None,
        };
    }

    public static bool AcceptsBoard(CasinoSeatSettle awaited, CasinoSeatSignal signal, long armedAtTick,
        long nowTick)
    {
        return Settles(awaited, signal) || nowTick - armedAtTick >= SettleGraceMilliseconds;
    }

    private static bool Settles(CasinoSeatSettle awaited, CasinoSeatSignal signal)
    {
        return awaited switch
        {
            CasinoSeatSettle.Bound => signal == CasinoSeatSignal.SeatBoundHere,
            CasinoSeatSettle.Released => signal == CasinoSeatSignal.SeatLost,
            _ => true,
        };
    }

    private static CasinoSeatStage FromWatching(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.SitRequested => CasinoSeatStage.Sitting,
            CasinoSeatSignal.SeatBoundHere => CasinoSeatStage.Seated,
            CasinoSeatSignal.SeatBoundElsewhere => CasinoSeatStage.Elsewhere,
            _ => CasinoSeatStage.Watching,
        };
    }

    private static CasinoSeatStage FromSitting(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.SitGranted => CasinoSeatStage.Seated,
            CasinoSeatSignal.SitRefused => CasinoSeatStage.Watching,
            CasinoSeatSignal.SeatBoundHere => CasinoSeatStage.Seated,
            CasinoSeatSignal.SeatBoundElsewhere => CasinoSeatStage.Elsewhere,
            _ => CasinoSeatStage.Sitting,
        };
    }

    private static CasinoSeatStage FromSeated(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.StandRequested => CasinoSeatStage.Standing,
            CasinoSeatSignal.SeatBoundElsewhere => CasinoSeatStage.Elsewhere,
            _ => CasinoSeatStage.Seated,
        };
    }

    private static CasinoSeatStage FromElsewhere(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.TakeOverRequested => CasinoSeatStage.Claiming,
            CasinoSeatSignal.SeatBoundHere => CasinoSeatStage.Seated,
            _ => CasinoSeatStage.Elsewhere,
        };
    }

    private static CasinoSeatStage FromClaiming(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.ClaimGranted => CasinoSeatStage.Seated,
            CasinoSeatSignal.ClaimRefused => CasinoSeatStage.Elsewhere,
            CasinoSeatSignal.SeatBoundHere => CasinoSeatStage.Seated,
            _ => CasinoSeatStage.Claiming,
        };
    }

    private static CasinoSeatStage FromStanding(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.StandGranted => CasinoSeatStage.Watching,
            CasinoSeatSignal.StandQueued => CasinoSeatStage.Seated,
            CasinoSeatSignal.StandRefused => CasinoSeatStage.Seated,
            CasinoSeatSignal.SeatBoundElsewhere => CasinoSeatStage.Elsewhere,
            _ => CasinoSeatStage.Standing,
        };
    }
}
