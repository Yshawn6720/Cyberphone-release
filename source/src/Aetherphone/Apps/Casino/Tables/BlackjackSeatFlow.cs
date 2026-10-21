using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackSeatFlow
{
    private readonly CasinoTablesStore tables;

    private CasinoSeatStage stage = CasinoSeatStage.Watching;
    private CasinoSeatSettle settle = CasinoSeatSettle.None;
    private string reason = string.Empty;
    private long settleArmedAtTick;
    private bool waitArmed;
    private bool standQueued;

    public BlackjackSeatFlow(CasinoTablesStore tables)
    {
        this.tables = tables;
    }

    public CasinoSeatStage Stage => stage;

    public string Reason => reason;

    public bool Waiting => waitArmed;

    public bool StandQueued => standQueued;

    public bool Busy => CasinoSeatMachine.Busy(stage) || tables.IntentInFlight;

    public void Reset()
    {
        stage = CasinoSeatStage.Watching;
        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        reason = string.Empty;
        waitArmed = false;
        standQueued = false;
    }

    public void ClearReason()
    {
        reason = string.Empty;
    }

    public void Observe(CasinoBlackjackRoomStateDto? board, int mySeat, int phase, long nowTick)
    {
        ConsumeOutcomes(nowTick);
        if (board is null)
        {
            return;
        }

        var hasSeat = BlackjackRules.IsSeat(mySeat);
        var signal = CasinoSeatMachine.SignalFor(hasSeat, false);
        if (!Settled(signal, nowTick))
        {
            return;
        }

        stage = CasinoSeatMachine.Next(stage, signal);
        if (!hasSeat)
        {
            waitArmed = false;
            standQueued = false;
            return;
        }

        if (CasinoJoinGate.ClearsWait(phase))
        {
            waitArmed = false;
        }

        waitArmed = CasinoJoinGate.Waiting(true, waitArmed, SeatOf(board, mySeat)?.JoinsNextHand ?? false);
        standQueued = standQueued || (SeatOf(board, mySeat)?.LeaveAtHandEnd ?? false);
    }

    private static CasinoBlackjackSeatDto? SeatOf(CasinoBlackjackRoomStateDto board, int seatIndex)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return null;
        }

        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].SeatIndex == seatIndex)
            {
                return seats[index];
            }
        }

        return null;
    }

    public void Sit(string roomId, int seatIndex, long buyIn, int phase)
    {
        if (Busy || CasinoSeatMachine.Holds(stage) || roomId.Length == 0)
        {
            return;
        }

        reason = string.Empty;
        waitArmed = CasinoJoinGate.ArmsWait(phase);
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.SitRequested);
        tables.Sit(roomId, seatIndex, buyIn);
    }

    public void Stand(string roomId)
    {
        if (Busy || roomId.Length == 0)
        {
            return;
        }

        reason = string.Empty;
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandRequested);
        tables.Stand(roomId);
    }

    public void Left()
    {
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.Left);
        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        waitArmed = false;
        standQueued = false;
    }

    private bool Settled(CasinoSeatSignal signal, long nowTick)
    {
        if (settle == CasinoSeatSettle.None)
        {
            return true;
        }

        if (!CasinoSeatMachine.AcceptsBoard(settle, signal, settleArmedAtTick, nowTick))
        {
            return false;
        }

        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        return true;
    }

    private void ConsumeOutcomes(long nowTick)
    {
        if (tables.TakeIntentFailure())
        {
            reason = CasinoReasons.Unreachable;
            settle = CasinoSeatSettle.None;
            settleArmedAtTick = 0;
            stage = RefusalOf(stage);
        }

        var outcome = tables.TakeSeatOutcome();
        if (outcome is null)
        {
            return;
        }

        if (!outcome.Granted)
        {
            reason = outcome.Reason;
            settle = CasinoSeatSettle.None;
            settleArmedAtTick = 0;
            stage = RefusalOf(stage);
            return;
        }

        reason = string.Empty;
        standQueued = outcome.AtHandEnd;
        if (outcome.JoinsNextHand)
        {
            waitArmed = true;
        }

        settle = CasinoSeatMachine.SettleFor(stage, outcome.AtHandEnd);
        settleArmedAtTick = nowTick;
        stage = CasinoSeatMachine.Next(stage, GrantOf(stage, outcome.AtHandEnd));
    }

    private static CasinoSeatSignal GrantOf(CasinoSeatStage stage, bool atHandEnd)
    {
        return stage switch
        {
            CasinoSeatStage.Sitting => CasinoSeatSignal.SitGranted,
            CasinoSeatStage.Claiming => CasinoSeatSignal.ClaimGranted,
            CasinoSeatStage.Standing => atHandEnd ? CasinoSeatSignal.StandQueued : CasinoSeatSignal.StandGranted,
            _ => CasinoSeatSignal.SeatBoundHere,
        };
    }

    private static CasinoSeatStage RefusalOf(CasinoSeatStage stage)
    {
        return stage switch
        {
            CasinoSeatStage.Sitting => CasinoSeatMachine.Next(stage, CasinoSeatSignal.SitRefused),
            CasinoSeatStage.Claiming => CasinoSeatMachine.Next(stage, CasinoSeatSignal.ClaimRefused),
            CasinoSeatStage.Standing => CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandRefused),
            _ => stage,
        };
    }
}
