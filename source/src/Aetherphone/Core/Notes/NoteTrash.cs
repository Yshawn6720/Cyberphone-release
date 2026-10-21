namespace Aetherphone.Core.Notes;

internal static class NoteTrash
{
    public const int RetentionDays = 30;

    public static void Discard(List<PhoneNote> notes, List<PhoneNote> trash, PhoneNote note, DateTime now)
    {
        notes.Remove(note);
        if (!note.HasContent)
        {
            return;
        }

        note.DeletedAt = now;
        trash.Remove(note);
        trash.Insert(0, note);
    }

    public static void Restore(List<PhoneNote> notes, List<PhoneNote> trash, PhoneNote note)
    {
        if (!trash.Remove(note))
        {
            return;
        }

        note.DeletedAt = null;
        notes.Insert(0, note);
    }

    public static bool Purge(List<PhoneNote> trash, DateTime now)
    {
        var removed = false;
        for (var index = trash.Count - 1; index >= 0; index--)
        {
            if (DaysLeft(trash[index], now) > 0)
            {
                continue;
            }

            trash.RemoveAt(index);
            removed = true;
        }

        return removed;
    }

    public static int DaysLeft(PhoneNote note, DateTime now)
    {
        var deletedAt = note.DeletedAt ?? now;
        var expiry = deletedAt.AddDays(RetentionDays);
        var remaining = expiry - now;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalDays);
    }
}
