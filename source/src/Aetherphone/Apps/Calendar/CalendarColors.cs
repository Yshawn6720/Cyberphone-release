using Aetherphone.Core.Calendar;

namespace Aetherphone.Apps.Calendar;

internal static class CalendarColors
{
    private static readonly Vector4[] Swatches =
    {
        new(1.000f, 0.231f, 0.188f, 1f),
        new(1.000f, 0.584f, 0.000f, 1f),
        new(1.000f, 0.800f, 0.000f, 1f),
        new(0.204f, 0.780f, 0.349f, 1f),
        new(0.000f, 0.478f, 1.000f, 1f),
        new(0.686f, 0.322f, 0.871f, 1f),
        new(1.000f, 0.176f, 0.333f, 1f),
        new(0.635f, 0.518f, 0.369f, 1f),
    };

    public static int Count => Swatches.Length;

    public static Vector4 At(int index) => Swatches[Math.Clamp(index, 0, Swatches.Length - 1)];

    public static Vector4 Resolve(int colorIndex, Vector4 accent) =>
        colorIndex >= 0 && colorIndex < Swatches.Length ? Swatches[colorIndex] : accent;

    public static Vector4 For(CalendarEventGroup? group, Vector4 accent) =>
        group is null ? accent : Resolve(group.ColorIndex, accent);

    public static int NextFree(IReadOnlyList<CalendarEventGroup> groups)
    {
        for (var candidate = 1; candidate < Swatches.Length; candidate++)
        {
            var used = false;
            for (var index = 0; index < groups.Count; index++)
            {
                if (groups[index].ColorIndex == candidate)
                {
                    used = true;
                    break;
                }
            }

            if (!used)
            {
                return candidate;
            }
        }

        return groups.Count % Swatches.Length;
    }
}
