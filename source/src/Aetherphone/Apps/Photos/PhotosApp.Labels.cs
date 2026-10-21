using System.Globalization;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private const int RecentWeekDays = 6;

    private readonly Dictionary<int, string> countLabels = new();
    private readonly Dictionary<int, string> numberLabels = new();
    private readonly Dictionary<int, string> selectedLabels = new();
    private readonly Dictionary<long, string> dayLabels = new();
    private readonly Dictionary<int, string> monthLabels = new();
    private readonly Dictionary<int, string> yearLabels = new();
    private readonly Dictionary<int, string> daysLeftLabels = new();
    private CultureInfo? labelCulture;
    private DateTime labelDay;
    private int labelTimeFormat = -1;

    private void SyncLabels()
    {
        var today = DateTime.Today;
        if (ReferenceEquals(labelCulture, Loc.Culture) && labelDay == today && labelTimeFormat == TimeText.FormatVersion)
        {
            return;
        }

        labelCulture = Loc.Culture;
        labelDay = today;
        labelTimeFormat = TimeText.FormatVersion;
        countLabels.Clear();
        numberLabels.Clear();
        selectedLabels.Clear();
        dayLabels.Clear();
        monthLabels.Clear();
        yearLabels.Clear();
        daysLeftLabels.Clear();
        Array.Clear(levelLabels);
        viewerTitlePath = string.Empty;
        sortRowKey = -1;
        InvalidateLayouts();
    }

    private string CountLabel(int count)
    {
        if (!countLabels.TryGetValue(count, out var label))
        {
            label = Loc.Plural(L.Photos.Count, count);
            countLabels[count] = label;
        }

        return label;
    }

    private string NumberLabel(int value)
    {
        if (!numberLabels.TryGetValue(value, out var label))
        {
            label = value.ToString(Loc.Culture);
            numberLabels[value] = label;
        }

        return label;
    }

    private string SelectedLabel(int count)
    {
        if (!selectedLabels.TryGetValue(count, out var label))
        {
            label = Loc.Plural(L.Photos.Selected, count);
            selectedLabels[count] = label;
        }

        return label;
    }

    private string DaysLeftLabel(int days)
    {
        if (!daysLeftLabels.TryGetValue(days, out var label))
        {
            label = Loc.Plural(L.Photos.DaysLeft, days);
            daysLeftLabels[days] = label;
        }

        return label;
    }

    private string DayTitle(DateTime taken)
    {
        var day = taken.Date;
        if (dayLabels.TryGetValue(day.Ticks, out var label))
        {
            return label;
        }

        label = DayText(day, labelDay, Loc.Culture);
        dayLabels[day.Ticks] = label;
        return label;
    }

    private static string DayText(DateTime day, DateTime today, CultureInfo culture)
    {
        if (day == today)
        {
            return Loc.T(L.Photos.Today);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Photos.Yesterday);
        }

        if (day > today.AddDays(-RecentWeekDays) && day <= today)
        {
            return Capitalize(culture.DateTimeFormat.GetDayName(day.DayOfWeek), culture);
        }

        return day.Year == today.Year
            ? day.ToString(culture.DateTimeFormat.MonthDayPattern, culture)
            : day.ToString("D", culture);
    }

    private string MonthTitle(int monthKey)
    {
        if (monthLabels.TryGetValue(monthKey, out var label))
        {
            return label;
        }

        var month = new DateTime(monthKey / 100, monthKey % 100, 1);
        label = Capitalize(month.ToString(Loc.Culture.DateTimeFormat.YearMonthPattern, Loc.Culture), Loc.Culture);
        monthLabels[monthKey] = label;
        return label;
    }

    private string YearTitle(int year)
    {
        if (!yearLabels.TryGetValue(year, out var label))
        {
            label = year.ToString(CultureInfo.InvariantCulture);
            yearLabels[year] = label;
        }

        return label;
    }

    private static string Capitalize(string text, CultureInfo culture) =>
        text.Length == 0 ? text : char.ToUpper(text[0], culture) + text.Substring(1);
}
