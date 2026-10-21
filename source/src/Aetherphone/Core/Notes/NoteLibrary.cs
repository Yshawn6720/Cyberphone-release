namespace Aetherphone.Core.Notes;

internal enum NoteBucket : byte
{
    Pinned,
    Today,
    Yesterday,
    PreviousWeek,
    PreviousMonth,
    Month,
    Year,
}

internal readonly record struct NoteSection(NoteBucket Bucket, int Year, int Month, int Start, int Count);

internal sealed class NoteLibrary
{
    private const int WeekDays = 7;
    private const int MonthDays = 30;

    private readonly List<int> order = new();
    private readonly List<NoteSection> sections = new();
    private readonly Comparison<int> compare;
    private List<PhoneNote> source = new();

    public NoteLibrary()
    {
        compare = Compare;
    }

    public IReadOnlyList<int> Order => order;

    public IReadOnlyList<NoteSection> Sections => sections;

    public int MatchCount => order.Count;

    public static NoteBucket Classify(DateTime updatedAt, DateTime today)
    {
        var day = updatedAt.Date;
        if (day >= today)
        {
            return NoteBucket.Today;
        }

        if (day == today.AddDays(-1))
        {
            return NoteBucket.Yesterday;
        }

        if (day > today.AddDays(-WeekDays))
        {
            return NoteBucket.PreviousWeek;
        }

        if (day > today.AddDays(-MonthDays))
        {
            return NoteBucket.PreviousMonth;
        }

        return day.Year == today.Year ? NoteBucket.Month : NoteBucket.Year;
    }

    public static bool Matches(PhoneNote note, string query) =>
        query.Length == 0 || note.Body.Contains(query, StringComparison.OrdinalIgnoreCase);

    public static long Fingerprint(List<PhoneNote> notes)
    {
        var hash = (long)notes.Count;
        for (var index = 0; index < notes.Count; index++)
        {
            var note = notes[index];
            hash = hash * 31 + note.UpdatedAt.Ticks;
            hash = hash * 31 + note.Id.GetHashCode();
            hash = hash * 31 + (note.Pinned ? 1 : 0);
        }

        return hash;
    }

    public void Build(List<PhoneNote> notes, DateTime today, string query)
    {
        source = notes;
        order.Clear();
        sections.Clear();
        for (var index = 0; index < notes.Count; index++)
        {
            if (Matches(notes[index], query))
            {
                order.Add(index);
            }
        }

        order.Sort(compare);
        for (var position = 0; position < order.Count; position++)
        {
            var note = notes[order[position]];
            var bucket = note.Pinned ? NoteBucket.Pinned : Classify(note.UpdatedAt, today);
            var year = bucket is NoteBucket.Month or NoteBucket.Year ? note.UpdatedAt.Year : 0;
            var month = bucket == NoteBucket.Month ? note.UpdatedAt.Month : 0;
            var last = sections.Count - 1;
            if (last >= 0 && sections[last].Bucket == bucket && sections[last].Year == year &&
                sections[last].Month == month)
            {
                sections[last] = sections[last] with { Count = sections[last].Count + 1 };
                continue;
            }

            sections.Add(new NoteSection(bucket, year, month, position, 1));
        }
    }

    private int Compare(int left, int right)
    {
        var leftNote = source[left];
        var rightNote = source[right];
        if (leftNote.Pinned != rightNote.Pinned)
        {
            return leftNote.Pinned ? -1 : 1;
        }

        var byDate = rightNote.UpdatedAt.CompareTo(leftNote.UpdatedAt);
        return byDate != 0 ? byDate : left.CompareTo(right);
    }
}
