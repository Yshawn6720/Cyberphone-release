using Aetherphone.Core.Notes;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NotesLogicTests
{
    private static readonly DateTime Wednesday = new(2026, 10, 7);

    private static PhoneNote Note(string body, DateTime updatedAt, bool pinned = false) =>
        new() { Body = body, UpdatedAt = updatedAt, Pinned = pinned };

    [Fact]
    public void BucketsFollowAppleDateGroups()
    {
        Assert.Equal(NoteBucket.Today, NoteLibrary.Classify(Wednesday.AddHours(9), Wednesday));
        Assert.Equal(NoteBucket.Yesterday, NoteLibrary.Classify(Wednesday.AddDays(-1).AddHours(23), Wednesday));
        Assert.Equal(NoteBucket.PreviousWeek, NoteLibrary.Classify(Wednesday.AddDays(-6), Wednesday));
        Assert.Equal(NoteBucket.PreviousMonth, NoteLibrary.Classify(Wednesday.AddDays(-7), Wednesday));
        Assert.Equal(NoteBucket.PreviousMonth, NoteLibrary.Classify(Wednesday.AddDays(-29), Wednesday));
        Assert.Equal(NoteBucket.Month, NoteLibrary.Classify(new DateTime(2026, 2, 1), Wednesday));
        Assert.Equal(NoteBucket.Year, NoteLibrary.Classify(new DateTime(2025, 12, 31), Wednesday));
    }

    [Fact]
    public void FutureStampsCountAsToday()
    {
        Assert.Equal(NoteBucket.Today, NoteLibrary.Classify(Wednesday.AddDays(3), Wednesday));
    }

    [Fact]
    public void PinnedNotesLeadAndTheRestSortNewestFirst()
    {
        var notes = new List<PhoneNote>
        {
            Note("old", Wednesday.AddDays(-40)),
            Note("pinned", Wednesday.AddDays(-90), true),
            Note("today", Wednesday.AddHours(8)),
            Note("later today", Wednesday.AddHours(10)),
        };
        var library = new NoteLibrary();

        library.Build(notes, Wednesday, string.Empty);

        Assert.Equal(new[] { 1, 3, 2, 0 }, library.Order);
        Assert.Equal(3, library.Sections.Count);
        Assert.Equal(new NoteSection(NoteBucket.Pinned, 0, 0, 0, 1), library.Sections[0]);
        Assert.Equal(new NoteSection(NoteBucket.Today, 0, 0, 1, 2), library.Sections[1]);
        Assert.Equal(new NoteSection(NoteBucket.Month, 2026, 8, 3, 1), library.Sections[2]);
    }

    [Fact]
    public void MonthsSplitIntoTheirOwnSections()
    {
        var notes = new List<PhoneNote>
        {
            Note("june", new DateTime(2026, 6, 10)),
            Note("may", new DateTime(2026, 5, 2)),
            Note("last year", new DateTime(2025, 3, 2)),
        };
        var library = new NoteLibrary();

        library.Build(notes, Wednesday, string.Empty);

        Assert.Equal(3, library.Sections.Count);
        Assert.Equal(6, library.Sections[0].Month);
        Assert.Equal(5, library.Sections[1].Month);
        Assert.Equal(NoteBucket.Year, library.Sections[2].Bucket);
        Assert.Equal(2025, library.Sections[2].Year);
    }

    [Fact]
    public void SearchMatchesAnywhereInTheBodyIgnoringCase()
    {
        var notes = new List<PhoneNote>
        {
            Note("Raid night\nBring Tinctures", Wednesday),
            Note("Shopping list", Wednesday),
        };
        var library = new NoteLibrary();

        library.Build(notes, Wednesday, "tincture");

        Assert.Equal(new[] { 0 }, library.Order);
        Assert.Equal(1, library.MatchCount);
    }

    [Fact]
    public void FingerprintChangesWhenAPinOrEditLands()
    {
        var note = Note("a", Wednesday);
        var notes = new List<PhoneNote> { note };
        var before = NoteLibrary.Fingerprint(notes);

        note.Pinned = true;
        var pinned = NoteLibrary.Fingerprint(notes);
        note.UpdatedAt = Wednesday.AddMinutes(1);

        Assert.NotEqual(before, pinned);
        Assert.NotEqual(pinned, NoteLibrary.Fingerprint(notes));
    }

    [Fact]
    public void PreviewSkipsTheTitleAndCollapsesBlankLines()
    {
        var note = Note("\n  Title line \n\n first   detail\n\nsecond", Wednesday);

        Assert.Equal("Title line", note.Title());
        Assert.Equal("first detail second", note.Preview());
    }

    [Fact]
    public void DiscardMovesNotesToTrashAndDropsEmptyOnes()
    {
        var kept = Note("keep me", Wednesday);
        var blank = Note("   \n ", Wednesday);
        var notes = new List<PhoneNote> { kept, blank };
        var trash = new List<PhoneNote>();

        NoteTrash.Discard(notes, trash, kept, Wednesday);
        NoteTrash.Discard(notes, trash, blank, Wednesday);

        Assert.Empty(notes);
        Assert.Single(trash);
        Assert.Equal(Wednesday, kept.DeletedAt);
    }

    [Fact]
    public void RestorePutsTheNoteBackOnTop()
    {
        var other = Note("other", Wednesday);
        var deleted = Note("deleted", Wednesday);
        var notes = new List<PhoneNote> { other };
        var trash = new List<PhoneNote>();
        NoteTrash.Discard(new List<PhoneNote> { deleted }, trash, deleted, Wednesday);

        NoteTrash.Restore(notes, trash, deleted);

        Assert.Same(deleted, notes[0]);
        Assert.Empty(trash);
        Assert.Null(deleted.DeletedAt);
    }

    [Fact]
    public void PurgeRemovesNotesOlderThanTheRetentionWindow()
    {
        var fresh = Note("fresh", Wednesday);
        var stale = Note("stale", Wednesday);
        var trash = new List<PhoneNote>();
        NoteTrash.Discard(new List<PhoneNote> { fresh }, trash, fresh, Wednesday.AddDays(-2));
        NoteTrash.Discard(new List<PhoneNote> { stale }, trash, stale, Wednesday.AddDays(-NoteTrash.RetentionDays));

        Assert.True(NoteTrash.Purge(trash, Wednesday));

        Assert.Equal(new[] { fresh }, trash);
        Assert.Equal(NoteTrash.RetentionDays - 2, NoteTrash.DaysLeft(fresh, Wednesday));
        Assert.False(NoteTrash.Purge(trash, Wednesday));
    }

    [Fact]
    public void LegacyNoteJsonLoadsUnpinnedAndNotDeleted()
    {
        const string json = """{"Id":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","Body":"Old note","UpdatedAt":"2026-01-02T03:04:05"}""";

        var note = Newtonsoft.Json.JsonConvert.DeserializeObject<PhoneNote>(json)!;

        Assert.Equal("Old note", note.Body);
        Assert.False(note.Pinned);
        Assert.Null(note.DeletedAt);
    }
}
