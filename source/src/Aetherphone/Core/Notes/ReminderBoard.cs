namespace Aetherphone.Core.Notes;

internal enum ReminderGroup : byte
{
    Overdue,
    Today,
    Scheduled,
    Anytime,
    Completed,
}

internal enum ReminderFilter : byte
{
    All,
    Today,
    Scheduled,
    Completed,
}

internal enum ReminderQuickDate : byte
{
    Today,
    Tomorrow,
    Weekend,
    NextWeek,
}

internal readonly record struct ReminderSection(ReminderGroup Group, int Start, int Count);

internal sealed class ReminderBoard
{
    public const int GroupCount = 5;
    private const int LastMinuteOfDay = 23 * 60 + 55;
    private const int MinutesPerHour = 60;

    private readonly List<int> order = new();
    private readonly List<ReminderSection> sections = new();
    private readonly int[] counts = new int[GroupCount];
    private readonly Comparison<int> compare;
    private List<ReminderItem> source = new();
    private DateTime now;
    private Guid lingering;

    public ReminderBoard()
    {
        compare = Compare;
    }

    public IReadOnlyList<int> Order => order;

    public IReadOnlyList<ReminderSection> Sections => sections;

    public int CountOf(ReminderGroup group) => counts[(int)group];

    public int OpenCount => counts[(int)ReminderGroup.Overdue] + counts[(int)ReminderGroup.Today] +
                            counts[(int)ReminderGroup.Scheduled] + counts[(int)ReminderGroup.Anytime];

    public static ReminderGroup Classify(ReminderItem reminder, DateTime now)
    {
        return reminder.Done ? ReminderGroup.Completed : OpenGroup(reminder.DueAt, now);
    }

    public static ReminderGroup OpenGroup(DateTime? dueAt, DateTime now)
    {
        if (dueAt is not { } due)
        {
            return ReminderGroup.Anytime;
        }

        if (due < now)
        {
            return ReminderGroup.Overdue;
        }

        return due.Date == now.Date ? ReminderGroup.Today : ReminderGroup.Scheduled;
    }

    public static bool Shows(ReminderFilter filter, ReminderGroup group) => filter switch
    {
        ReminderFilter.Today => group is ReminderGroup.Overdue or ReminderGroup.Today,
        ReminderFilter.Scheduled => group is ReminderGroup.Overdue or ReminderGroup.Today or ReminderGroup.Scheduled,
        ReminderFilter.Completed => group == ReminderGroup.Completed,
        _ => true,
    };

    public static DateTime QuickDate(ReminderQuickDate kind, DateTime today)
    {
        var day = today.Date;
        var weekday = (int)day.DayOfWeek;
        return kind switch
        {
            ReminderQuickDate.Tomorrow => day.AddDays(1),
            ReminderQuickDate.Weekend => day.AddDays(((int)DayOfWeek.Saturday - weekday + 7) % 7),
            ReminderQuickDate.NextWeek => day.AddDays(DaysUntilNextMonday(weekday)),
            _ => day,
        };
    }

    public static int DefaultMinuteOfDay(DateTime now) =>
        Math.Min((now.Hour + 1) * MinutesPerHour, LastMinuteOfDay);

    private static int DaysUntilNextMonday(int weekday)
    {
        var days = ((int)DayOfWeek.Monday - weekday + 7) % 7;
        return days == 0 ? 7 : days;
    }

    public void Build(List<ReminderItem> reminders, DateTime current, ReminderFilter filter, Guid lingeringId)
    {
        source = reminders;
        now = current;
        lingering = lingeringId;
        order.Clear();
        sections.Clear();
        Array.Clear(counts);
        for (var index = 0; index < reminders.Count; index++)
        {
            var group = GroupOf(reminders[index]);
            counts[(int)group]++;
            if (Shows(filter, group))
            {
                order.Add(index);
            }
        }

        order.Sort(compare);
        for (var position = 0; position < order.Count; position++)
        {
            var group = GroupOf(reminders[order[position]]);
            var last = sections.Count - 1;
            if (last >= 0 && sections[last].Group == group)
            {
                sections[last] = sections[last] with { Count = sections[last].Count + 1 };
                continue;
            }

            sections.Add(new ReminderSection(group, position, 1));
        }
    }

    private ReminderGroup GroupOf(ReminderItem reminder)
    {
        var lingers = reminder.Done && lingering != Guid.Empty && reminder.Id == lingering;
        return lingers ? OpenGroup(reminder.DueAt, now) : Classify(reminder, now);
    }

    private int Compare(int left, int right)
    {
        var leftItem = source[left];
        var rightItem = source[right];
        var leftGroup = GroupOf(leftItem);
        var rightGroup = GroupOf(rightItem);
        if (leftGroup != rightGroup)
        {
            return leftGroup.CompareTo(rightGroup);
        }

        if (leftGroup is ReminderGroup.Overdue or ReminderGroup.Today or ReminderGroup.Scheduled &&
            leftItem.DueAt is { } leftDue && rightItem.DueAt is { } rightDue)
        {
            var byDue = leftDue.CompareTo(rightDue);
            if (byDue != 0)
            {
                return byDue;
            }
        }

        return left.CompareTo(right);
    }
}
