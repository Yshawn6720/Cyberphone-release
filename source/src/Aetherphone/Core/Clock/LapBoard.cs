namespace Aetherphone.Core.Clock;

internal static class LapBoard
{
    public static double Split(List<double> cumulative, int index) =>
        index <= 0 ? cumulative[0] : cumulative[index] - cumulative[index - 1];

    public static void Extremes(List<double> cumulative, out int best, out int worst)
    {
        best = -1;
        worst = -1;
        if (cumulative.Count < 2)
        {
            return;
        }

        var bestSplit = double.MaxValue;
        var worstSplit = double.MinValue;
        for (var index = 0; index < cumulative.Count; index++)
        {
            var split = Split(cumulative, index);
            if (split < bestSplit)
            {
                bestSplit = split;
                best = index;
            }

            if (split > worstSplit)
            {
                worstSplit = split;
                worst = index;
            }
        }

        if (best == worst)
        {
            best = -1;
            worst = -1;
        }
    }
}
