namespace Aetherphone.Core.Muster;

internal static class MusterSchedule
{
    public const int StepMinutes = 15;
    public const int MaxLeadMinutes = 480;
    public const int MinDurationMinutes = 30;
    public const int MaxDurationMinutes = 480;
    public const int DefaultLeadMinutes = 0;
    public const int DefaultDurationMinutes = 60;

    public static int LeadFromFraction(float fraction) => Snap(fraction, 0, MaxLeadMinutes);

    public static int DurationFromFraction(float fraction) =>
        Snap(fraction, MinDurationMinutes, MaxDurationMinutes);

    public static float LeadFraction(int minutes) => Fraction(minutes, 0, MaxLeadMinutes);

    public static float DurationFraction(int minutes) =>
        Fraction(minutes, MinDurationMinutes, MaxDurationMinutes);

    public static int ClampLead(int minutes) => Math.Clamp(RoundToStep(minutes), 0, MaxLeadMinutes);

    public static int ClampDuration(int minutes) =>
        Math.Clamp(RoundToStep(minutes), MinDurationMinutes, MaxDurationMinutes);

    private static int Snap(float fraction, int minimum, int maximum)
    {
        if (float.IsNaN(fraction))
        {
            return minimum;
        }

        var clamped = Math.Clamp(fraction, 0f, 1f);
        var raw = minimum + clamped * (maximum - minimum);
        return Math.Clamp(RoundToStep((int)MathF.Round(raw)), minimum, maximum);
    }

    private static int RoundToStep(int minutes)
    {
        var steps = (int)MathF.Round(minutes / (float)StepMinutes, MidpointRounding.AwayFromZero);
        return steps * StepMinutes;
    }

    private static float Fraction(int minutes, int minimum, int maximum)
    {
        if (maximum <= minimum)
        {
            return 0f;
        }

        return Math.Clamp((minutes - minimum) / (float)(maximum - minimum), 0f, 1f);
    }
}
