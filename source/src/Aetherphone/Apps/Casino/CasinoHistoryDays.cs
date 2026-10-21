using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino;

internal readonly record struct HistoryDay(int Start, int End, long Net, bool Complete, bool Settled);

internal static class CasinoHistoryDays
{
    public static long NetOf(CasinoRoundHistoryDto round) =>
        round.State == CasinoRoundStates.Settled ? round.Payout - round.Stake : 0;

    public static void Group(CasinoRoundHistoryDto[] rounds, bool hasMore, Func<long, long, bool> sameDay,
        List<HistoryDay> days)
    {
        days.Clear();
        var index = 0;
        while (index < rounds.Length)
        {
            var start = index;
            var net = 0L;
            var settled = false;
            while (index < rounds.Length && sameDay(rounds[start].CreatedAtUnix, rounds[index].CreatedAtUnix))
            {
                var round = rounds[index];
                net += NetOf(round);
                settled |= round.State == CasinoRoundStates.Settled;
                index++;
            }

            var complete = index < rounds.Length || !hasMore;
            days.Add(new HistoryDay(start, index, net, complete, settled));
        }
    }
}
