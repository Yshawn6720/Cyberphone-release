using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum DailySpinClaim
{
    Unknown,
    Available,
    Claimed,
    Denied,
}

internal static class DailySpinStatus
{
    public static DailySpinClaim Of(CasinoDailySpinDto? answer)
    {
        if (answer is null)
        {
            return DailySpinClaim.Unknown;
        }

        if (answer.Granted
            || answer.Claimed
            || string.Equals(answer.Reason, CasinoReasons.AlreadyClaimed, StringComparison.Ordinal))
        {
            return DailySpinClaim.Claimed;
        }

        return answer.Reason.Length == 0 ? DailySpinClaim.Available : DailySpinClaim.Denied;
    }

    public static bool CanClaim(CasinoDailySpinDto? answer, bool inFlight)
    {
        return !inFlight && Of(answer) != DailySpinClaim.Claimed;
    }

    public static bool ShowsReset(DailySpinClaim claim)
    {
        return claim == DailySpinClaim.Claimed;
    }

    public static bool OffersWheel(DailySpinClaim claim)
    {
        return claim == DailySpinClaim.Available;
    }

    public static long AwardOf(CasinoDailySpinDto? answer)
    {
        return answer is null ? 0 : answer.Amount;
    }
}
